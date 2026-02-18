using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using NetBricks;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueReaderForInference(
    IConfigFactory<IConfig> configFactory,
    IHttpClientFactory httpClientFactory,
    DefaultAzureCredential defaultAzureCredential,
    JobStatusService jobStatusService,
    ILogger<AzureStorageQueueReaderForInference> logger)
    : AzureStorageQueueReaderBase(configFactory, httpClientFactory, defaultAzureCredential, logger)
{
    private readonly IConfigFactory<IConfig> configFactory = configFactory;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly JobStatusService jobStatusService = jobStatusService;
    private readonly ILogger<AzureStorageQueueReaderForInference> logger = logger;
    private readonly List<QueueClient> inboundQueues = [];
    private readonly List<QueueClient> inboundDeadletterQueues = [];
    private TaskRunner? taskRunner;
    private QueueClient? outboundQueue;

    private async Task<bool> ProcessRequestAsync(
         QueueClient inboundQueue,
         QueueClient inboundDeadletterQueue,
         CancellationToken cancellationToken)
    {
        var isConsideredToHaveProcessed = false;
        try
        {
            // get config
            var config = await this.configFactory.GetAsync(cancellationToken);

            // check for a message
            this.logger.LogDebug("checking for a message in queue {q}...", inboundQueue.Name);
            var message = await inboundQueue.ReceiveMessageAsync(
                TimeSpan.FromSeconds(config.DEQUEUE_FOR_X_SECONDS),
                cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return isConsideredToHaveProcessed;
            }

            // handle deadletter
            if (message!.Value.DequeueCount > config.MAX_ATTEMPTS_TO_DEQUEUE)
            {
                throw new DeadletterException(
                    $"message {message.Value.MessageId} has been dequeued {message.Value.DequeueCount} times.",
                    message.Value,
                    body);
            }

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");
            using var activity = DiagnosticService.Source.StartActivity("process-inference", ActivityKind.Internal, request.Id);
            activity?.AddTagsFromPipelineRequest(request);

            // it is considered to have processed once it starts doing something related to the actual request
            isConsideredToHaveProcessed = true;

            // ensure required config values are present
            var inferenceContainer = config.INFERENCE_CONTAINER
                ?? throw new InvalidOperationException("INFERENCE_CONTAINER must be set for inference processing.");
            var inferenceUrl = config.INFERENCE_URL
                ?? throw new InvalidOperationException("INFERENCE_URL must be set for inference processing.");

            // download and transform the ground truth file
            var groundTruthBlobRef = new BlobRef(request.GroundTruthUri);
            var groundTruthBlobClient = await this.GetBlobClientAsync(groundTruthBlobRef.Container, groundTruthBlobRef.BlobName, cancellationToken);
            var groundTruthContent = await groundTruthBlobClient.DownloadAndTransformAsync(
                config.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // call processing URL
            var (responseHeaders, responseContent) = await this.SendForProcessingAsync(
                request,
                inferenceUrl,
                groundTruthContent,
                message.Value,
                body,
                request.InferenceHeaders,
                cancellationToken);

            // upload the result
            var inferenceUri = await this.UploadBlobAsync(
                inferenceContainer,
                $"{request.RunId}/{request.Id}.json",
                responseContent,
                cancellationToken);

            // handle the response headers (metrics, etc.)
            if (config.PROCESS_METRICS_IN_INFERENCE_RESPONSE)
            {
                await this.HandleResponseAsync(request, responseContent, inferenceUri, null, cancellationToken);
            }

            // enqueue for the next stage
            await this.outboundQueue!.SendMessageAsync(body, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);

            // record success for job status tracking
            await this.jobStatusService.RecordOutcomeAsync(request.RunId, request.Id, JobStage.Inference, JobOutcome.Success, null, cancellationToken);

            return true;
        }
        catch (DeadletterException e)
        {
            this.logger.LogWarning("{err}; moving to dead-letter queue {q}...", e.Message, inboundDeadletterQueue.Name);
            await inboundDeadletterQueue.SendMessageAsync(e.QueueBody, cancellationToken);
            await inboundQueue.DeleteMessageAsync(e.QueueMessage.MessageId, e.QueueMessage.PopReceipt, cancellationToken);
            this.logger.LogWarning("successfully moved message {m} to dead-letter queue {q}.", e.QueueMessage.MessageId, inboundDeadletterQueue.Name);

            // record failure for job status tracking
            var deadletterRequest = JsonConvert.DeserializeObject<PipelineRequest>(e.QueueBody);
            if (deadletterRequest != null)
            {
                await this.jobStatusService.RecordOutcomeAsync(deadletterRequest.RunId, deadletterRequest.Id, JobStage.Inference, JobOutcome.Failed, e.Message, cancellationToken);
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error processing message from queue {q}...", inboundQueue.Name);
        }
        return isConsideredToHaveProcessed;
    }

    private async Task<int> GetMessagesFromInboundQueuesAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        try
        {
            for (int i = 0; i < this.inboundQueues.Count; i++)
            {
                var queue = this.inboundQueues[i];
                var deadletter = this.inboundDeadletterQueues[i];
                var runner = this.taskRunner ?? throw new InvalidOperationException("task runner not initialized.");
                await runner.StartAsync(() =>
                    this.ProcessRequestAsync(queue, deadletter, cancellationToken),
                    onSuccess: async isConsideredToHaveProcessed =>
                    {
                        if (isConsideredToHaveProcessed)
                        {
                            count++;
                            await this.DelayAfterDequeue(cancellationToken);
                        }
                    });
            }
        }
        catch (OperationCanceledException)
        {
            // ignore; this is expected when stopping
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error getting messages from queues in AzureStorageQueueReaderForInference...");
        }
        return count;
    }

    public async Task<Dictionary<string, int>> GetAllQueueMessageCountsAsync()
    {
        List<QueueClient> queueClients = [.. this.inboundQueues, .. this.inboundDeadletterQueues];
        return await base.GetAllQueueMessageCountsAsync(queueClients);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("starting to listen for pipeline requests in AzureStorageQueueReaderForInference...");
        while (!stoppingToken.IsCancellationRequested)
        {
            var messagesFound = await this.GetMessagesFromInboundQueuesAsync(stoppingToken);
            if (messagesFound == 0)
            {
                await this.DelayAfterEmpty(stoppingToken);
            }
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = await this.configFactory.GetAsync(cancellationToken);
        if (!config.ROLES.Contains(Roles.InferenceProxy))
        {
            this.logger.LogInformation("InferenceProxy role not configured; skipping AzureStorageQueueReaderForInference.");
            return;
        }

        this.taskRunner = new TaskRunner(config.INFERENCE_CONCURRENCY);

        // try and connect to all the inbound inference queues
        foreach (var queue in config.INBOUND_INFERENCE_QUEUES)
        {
            var queueUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
                : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, queue);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundQueues.Add(queueClient);

            var deadletterUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}-deadletter";
            var deadletterClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(deadletterUrl), this.defaultAzureCredential)
                : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, queue + "-deadletter");
            await deadletterClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundDeadletterQueues.Add(deadletterClient);
        }

        // try and connect to the outbound inference queue
        if (!string.IsNullOrEmpty(config.OUTBOUND_INFERENCE_QUEUE))
        {
            var queueUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{config.OUTBOUND_INFERENCE_QUEUE}";
            var queueClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
                : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, config.OUTBOUND_INFERENCE_QUEUE);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.outboundQueue = queueClient;
        }

        await base.StartAsync(cancellationToken);
    }
}