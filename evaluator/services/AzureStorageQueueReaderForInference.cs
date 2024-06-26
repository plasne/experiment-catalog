using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueReaderForInference(IConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger<AzureStorageQueueReaderForInference> logger,
    DefaultAzureCredential? defaultAzureCredential = null)
    : AzureStorageQueueReaderBase(config, httpClientFactory, logger, defaultAzureCredential)
{
    private readonly List<QueueClient> inboundQueues = [];
    private readonly List<QueueClient> inboundDeadletterQueues = [];
    private readonly TaskRunner taskRunner = new(config.INFERENCE_CONCURRENCY);
    private QueueClient? outboundQueue;

    private async Task<bool> ProcessRequestAsync(
         QueueClient inboundQueue,
         QueueClient inboundDeadletterQueue,
         CancellationToken cancellationToken)
    {
        var isConsideredToHaveProcessed = false;
        try
        {
            // check for a message
            this.logger.LogDebug("checking for a message in queue {q}...", inboundQueue.Name);
            var message = await inboundQueue.ReceiveMessageAsync(TimeSpan.FromSeconds(this.config.DEQUEUE_FOR_X_SECONDS), cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return isConsideredToHaveProcessed;
            }

            // handle deadletter
            if (message!.Value.DequeueCount > this.config.MAX_ATTEMPTS_TO_DEQUEUE)
            {
                throw new DeadletterException($"message {message.Value.MessageId} has been dequeued {message.Value.DequeueCount} times.", message.Value, body);
            }

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");
            using var activity = DiagnosticService.Source.StartActivity("process-inference", ActivityKind.Internal, request.Id);
            activity?.AddTagsFromPipelineRequest(request);

            // it is considered to have processed once it starts doing something related to the actual request
            isConsideredToHaveProcessed = true;

            // download and transform the ground truth file
            var groundTruthBlobRef = new BlobRef(request.GroundTruthUri);
            var groundTruthBlobClient = this.GetBlobClient(groundTruthBlobRef.Container, groundTruthBlobRef.BlobName);
            var groundTruthContent = await groundTruthBlobClient.DownloadAndTransformAsync(
                this.config.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // call processing URL
            var (responseHeaders, responseContent) = await this.SendForProcessingAsync(
                request,
                this.config.INFERENCE_URL,
                groundTruthContent,
                message.Value,
                body,
                request.InferenceHeaders,
                cancellationToken);

            // upload the result
            var inferenceUri = await this.UploadBlobAsync(this.config.INFERENCE_CONTAINER, $"{request.RunId}/{request.Id}.json", responseContent, cancellationToken);

            // handle the response headers (metrics, histograms, etc.)
            await this.HandleResponseHeadersAsync(request, responseHeaders, inferenceUri, null, cancellationToken);

            // enqueue for the next stage
            await this.outboundQueue!.SendMessageAsync(body, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (DeadletterException e)
        {
            this.logger.LogWarning("{err}; moving to dead-letter queue {q}...", e.Message, inboundDeadletterQueue.Name);
            await inboundDeadletterQueue.SendMessageAsync(e.QueueBody, cancellationToken);
            await inboundQueue.DeleteMessageAsync(e.QueueMessage.MessageId, e.QueueMessage.PopReceipt, cancellationToken);
            this.logger.LogWarning("successfully moved message {m} to dead-letter queue {q}.", e.QueueMessage.MessageId, inboundDeadletterQueue.Name);
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
                await this.taskRunner.StartAsync(() =>
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
        // try and connect to all the inbound inference queues
        foreach (var queue in this.config.INBOUND_INFERENCE_QUEUES)
        {
            var queueUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
                : new QueueClient(this.config.AZURE_STORAGE_CONNECTION_STRING, queue);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundQueues.Add(queueClient);

            var deadletterUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}-deadletter";
            var deadletterClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(deadletterUrl), this.defaultAzureCredential)
                : new QueueClient(this.config.AZURE_STORAGE_CONNECTION_STRING, queue + "-deadletter");
            await deadletterClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundDeadletterQueues.Add(deadletterClient);
        }

        // try and connect to the outbound inference queue
        if (!string.IsNullOrEmpty(this.config.OUTBOUND_INFERENCE_QUEUE))
        {
            var queueUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{this.config.OUTBOUND_INFERENCE_QUEUE}";
            var queueClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
                : new QueueClient(this.config.AZURE_STORAGE_CONNECTION_STRING, this.config.OUTBOUND_INFERENCE_QUEUE);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.outboundQueue = queueClient;
        }

        await base.StartAsync(cancellationToken);
    }
}