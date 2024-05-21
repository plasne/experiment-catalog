using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueReader(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    IHttpClientFactory httpClientFactory,
    ILogger<AzureStorageQueueReader> logger)
    : BackgroundService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ILogger<AzureStorageQueueReader> logger = logger;
    private readonly List<QueueClient> inboundInferenceQueues = [];
    private readonly List<QueueClient> inboundInferenceDeadletterQueues = [];
    private readonly List<QueueClient> inboundEvaluationQueues = [];
    private readonly List<QueueClient> inboundEvaluationDeadletterQueues = [];
    private QueueClient? outboundInferenceQueue;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // try and connect to all the inbound inference queues
        foreach (var queue in this.config.INBOUND_INFERENCE_QUEUES)
        {
            var queueUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = new QueueClient(new Uri(queueUrl), this.defaultAzureCredential);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundInferenceQueues.Add(queueClient);

            var deadletterUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}-deadletter";
            var deadletterClient = new QueueClient(new Uri(deadletterUrl), this.defaultAzureCredential);
            await deadletterClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundInferenceDeadletterQueues.Add(deadletterClient);
        }

        // try and connect to all the inbound evaluation queues
        foreach (var queue in this.config.INBOUND_EVALUATION_QUEUES)
        {
            var queueUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = new QueueClient(new Uri(queueUrl), this.defaultAzureCredential);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundEvaluationQueues.Add(queueClient);

            var deadletterUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}-deadletter";
            var deadletterClient = new QueueClient(new Uri(deadletterUrl), this.defaultAzureCredential);
            await deadletterClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundEvaluationDeadletterQueues.Add(deadletterClient);
        }

        // try and connect to the outbound inference queue
        if (!string.IsNullOrEmpty(this.config.OUTBOUND_INFERENCE_QUEUE))
        {
            var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{this.config.OUTBOUND_INFERENCE_QUEUE}";
            var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.outboundInferenceQueue = queueClient;
        }

        await base.StartAsync(cancellationToken);
    }

    private BlobClient GetBlobClient(string containerName, string blobName)
    {
        string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        BlobServiceClient blobServiceClient = new(new Uri(blobServiceUri), this.defaultAzureCredential);
        BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
        return blobContainerClient.GetBlobClient(blobName);
    }

    private async Task<string> UploadBlobAsync(string containerName, string blobName, string content, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to upload {c}/{b}...", containerName, blobName);
        var blobClient = this.GetBlobClient(containerName, blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blobClient.UploadAsync(stream, cancellationToken);
        this.logger.LogInformation("successfully uploaded {c}/{b}.", containerName, blobName);
        return blobClient.Uri.ToString();
    }

    private string GetBlobUri(string containerName, string blobName)
    {
        var blobClient = this.GetBlobClient(containerName, blobName);
        return blobClient.Uri.ToString();
    }

    private async Task RecordMetricsAsync(
        PipelineRequest pipelineRequest,
        string? inferenceUri,
        string? evaluationUri,
        Dictionary<string, decimal> metrics,
        CancellationToken cancellationToken)
    {
        if (metrics.Count == 0)
        {
            return;
        }
        if (string.IsNullOrEmpty(this.config.EXPERIMENT_CATALOG_BASE_URL))
        {
            this.logger.LogWarning("there is no EXPERIMENT_CATALOG_BASE_URL provided, so no metrics will be logged.");
            return;
        }

        this.logger.LogDebug("attempting to record {x} metrics...", metrics.Count);
        using var httpClient = this.httpClientFactory.CreateClient();
        var result = new Result
        {
            Ref = pipelineRequest.Ref,
            Set = pipelineRequest.Set,
            InferenceUri = inferenceUri,
            EvaluationUri = evaluationUri,
            Metrics = metrics,
            IsBaseline = false
        };
        var resultJson = JsonConvert.SerializeObject(result);
        var response = await httpClient.PostAsync(
            $"{this.config.EXPERIMENT_CATALOG_BASE_URL}/api/projects/{pipelineRequest.Project}/experiments/{pipelineRequest.Experiment}/results",
            new StringContent(resultJson, Encoding.UTF8, "application/json"),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            this.logger.LogError("when trying to record metrics, got HTTP {c}: {s}.", response.StatusCode, content);
        }
        this.logger.LogInformation("successfully recorded {x} metrics ({y}).",
            metrics.Count,
            string.Join(", ", metrics.Select(x => x.Key)));
    }

    private void RecordHistogramsAsync(PipelineRequest pipelineRequest, List<string> connectionStrings)
    {
        if (connectionStrings.Count == 0)
        {
            return;
        }

        this.logger.LogDebug("attempting to record {x} histograms...", connectionStrings.Count);

        var recorded = new Dictionary<string, decimal>();
        var notRecorded = new List<string>();
        var meter = new Meter(DiagnosticService.SourceName);
        foreach (var connectionString in connectionStrings)
        {
            var definition = new HistogramDefinition(connectionString);
            if (definition.TryRecord(meter, pipelineRequest))
            {
                recorded.Add(definition.Name!, definition.Value ?? 0);
            }
            else
            {
                notRecorded.Add(definition.Name!);
            }
        }

        this.logger.LogInformation(
            "successfully recorded {r} histograms ({h}); not recorded ({n}).",
            recorded.Count,
            string.Join(", ", recorded.Select(x => $"{x.Key}={x.Value}")),
            string.Join(", ", notRecorded));
    }

    private async Task HandleResponseHeadersAsync(
        PipelineRequest pipelineRequest,
        HttpResponseHeaders headers,
        string? inferenceUri,
        string? evaluationUri,
        CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, decimal>();
        var connectionStrings = new List<string>();

        // look at all the headers
        foreach (var header in headers)
        {
            if (header.Value is null || !header.Value.Any() || string.IsNullOrEmpty(header.Value.First())) continue;
            var value = header.Value.First();

            if (header.Key.StartsWith("x-tag-", StringComparison.InvariantCultureIgnoreCase))
            {
                Activity.Current?.AddTag(header.Key[6..], value);
            }
            else if (header.Key.StartsWith("x-metric-", StringComparison.InvariantCultureIgnoreCase))
            {
                var key = header.Key[9..];
                if (decimal.TryParse(value, out var dvalue))
                {
                    metrics.Add(key, dvalue);
                    Activity.Current?.AddTag(key, value);
                }
            }
            else if (header.Key.StartsWith("x-histogram-", StringComparison.InvariantCultureIgnoreCase))
            {
                connectionStrings.Add(header.Value.First());
            }
        }

        // record
        await this.RecordMetricsAsync(pipelineRequest, inferenceUri, evaluationUri, metrics, cancellationToken);
        this.RecordHistogramsAsync(pipelineRequest, connectionStrings);
    }

    private async Task<(HttpResponseHeaders, string)> SendForProcessingAsync(
        PipelineRequest pipelineRequest,
        string url,
        string content,
        QueueMessage queueMessage,
        string queueBody,
        CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to call '{u}' for processing...", url);

        // call the processing endpoint
        using var httpClient = this.httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-run-id", pipelineRequest.RunId.ToString());
        var response = await httpClient.SendAsync(request, cancellationToken);

        // validate the response
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (this.config.BACKOFF_ON_STATUS_CODES.Contains((int)response.StatusCode))
        {
            var ms = response.Headers.RetryAfter?.Delta is not null
                ? (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds
                : this.config.MS_TO_ADD_ON_BUSY;
            if (ms > 0)
            {
                this.config.MS_BETWEEN_DEQUEUE_CURRENT += ms;
                this.logger.LogWarning(
                    "received {code}; delaying {ms} ms for a MS_BETWEEN_DEQUEUE of {total} ms.",
                    response.StatusCode,
                    ms,
                    this.config.MS_BETWEEN_DEQUEUE_CURRENT);
            }
        }
        if (this.config.DEADLETTER_ON_STATUS_CODES.Contains((int)response.StatusCode))
        {
            throw new DeadletterException($"status code {response.StatusCode} is considered fatal", queueMessage, queueBody);
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"calling {url} resulted in {response.StatusCode}: {responseBody}.");
        }
        if (string.IsNullOrEmpty(responseBody))
        {
            throw new Exception("response body is empty.");
        }

        // log
        this.logger.LogInformation("successfully called '{u}' for processing.", url);
        return (response.Headers, responseBody);
    }

    private async Task<bool> ProcessInferenceRequestAsync(
        QueueClient inboundQueue,
        QueueClient inboundDeadletterQueue,
        CancellationToken cancellationToken)
    {
        try
        {
            // check for a message
            this.logger.LogDebug("checking for a message in queue {q}...", inboundQueue.Name);
            var message = inboundQueue.ReceiveMessage(TimeSpan.FromSeconds(this.config.DEQUEUE_FOR_X_SECONDS), cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            // handle deadletter
            if (message!.Value.DequeueCount > this.config.MAX_ATTEMPTS_TO_DEQUEUE)
            {
                throw new DeadletterException($"message {message.Value.MessageId} has been dequeued {message.Value.DequeueCount} times", message.Value, body);
            }

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");
            using var activity = DiagnosticService.Source.StartActivity("process-inference", ActivityKind.Internal, request.Id);
            activity?.AddTagsFromPipelineRequest(request);

            // download and transform the ground truth file
            var groundTruthBlobRef = new BlobRef(request.GroundTruthUri);
            var groundTruthBlobClient = this.GetBlobClient(groundTruthBlobRef.Container, groundTruthBlobRef.BlobName);
            var groundTruthContent = await groundTruthBlobClient.DownloadAndTransformAsync(
                this.config.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // call processing URL
            var (responseHeaders, responseContent) = await this.SendForProcessingAsync(
                request,
                this.config.INFERENCE_URL,
                groundTruthContent,
                message.Value,
                body,
                cancellationToken);

            // upload the result
            var inferenceUri = await this.UploadBlobAsync(this.config.INFERENCE_CONTAINER, request.Id + ".json", responseContent, cancellationToken);

            // handle the response headers (metrics, histograms, etc.)
            await HandleResponseHeadersAsync(request, responseHeaders, inferenceUri, null, cancellationToken);

            // enqueue for the next stage
            await this.outboundInferenceQueue!.SendMessageAsync(body, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (DeadletterException e)
        {
            this.logger.LogWarning("{err}; moving to dead-letter queue...", e.Message);
            await inboundDeadletterQueue.SendMessageAsync(e.QueueBody, cancellationToken);
            await inboundQueue.DeleteMessageAsync(e.QueueMessage.MessageId, e.QueueMessage.PopReceipt, cancellationToken);
            this.logger.LogWarning("successfully moved message {m} to dead-letter queue.", e.QueueMessage.MessageId);
            return false;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error processing message from queue {q}...", inboundQueue.Name);
            return true;
        }
    }

    private async Task<bool> ProcessEvaluationRequestAsync(
        QueueClient inboundQueue,
        QueueClient inboundDeadletterQueue,
        CancellationToken cancellationToken)
    {
        try
        {
            // check for a message
            this.logger.LogDebug("checking for a message in queue {q}...", inboundQueue.Name);
            var message = inboundQueue.ReceiveMessage(TimeSpan.FromSeconds(this.config.DEQUEUE_FOR_X_SECONDS), cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            // handle deadletter
            if (message!.Value.DequeueCount > this.config.MAX_ATTEMPTS_TO_DEQUEUE)
            {
                throw new DeadletterException($"message {message.Value.MessageId} has been dequeued {message.Value.DequeueCount} times", message.Value, body);
            }

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");
            using var activity = DiagnosticService.Source.StartActivity("process-evaluation", ActivityKind.Internal, request.Id);
            activity?.AddTagsFromPipelineRequest(request);

            // download and transform the ground truth file
            var groundTruthBlobRef = new BlobRef(request.GroundTruthUri);
            var groundTruthBlobClient = this.GetBlobClient(groundTruthBlobRef.Container, groundTruthBlobRef.BlobName);
            var groundTruthContent = await groundTruthBlobClient.DownloadAndTransformAsync(
                this.config.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // download and transform the inference file
            var inferenceBlobClient = this.GetBlobClient(this.config.INFERENCE_CONTAINER, request.Id + ".json");
            var inferenceContent = await inferenceBlobClient.DownloadAndTransformAsync(
                this.config.INBOUND_INFERENCE_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // generate consolidated payload
            string payload = JsonConvert.SerializeObject(new
            {
                ground_truth = JsonConvert.DeserializeObject(groundTruthContent),
                inference = JsonConvert.DeserializeObject(inferenceContent)
            });

            // call processing URL
            var (responseHeaders, responseContent) = await this.SendForProcessingAsync(
                request,
                this.config.EVALUATION_URL,
                payload,
                message.Value,
                body,
                cancellationToken);

            // upload the result
            var evaluationUri = await this.UploadBlobAsync(this.config.EVALUATION_CONTAINER, request.Id + ".json", responseContent, cancellationToken);

            // get reference to the inferenceUri
            var inferenceUri = string.IsNullOrEmpty(this.config.INFERENCE_CONTAINER)
                ? null
                : this.GetBlobUri(this.config.INFERENCE_CONTAINER, request.Id + ".json");

            // handle the response headers (metrics, histograms, etc.)
            await HandleResponseHeadersAsync(request, responseHeaders, inferenceUri, evaluationUri, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (DeadletterException e)
        {
            this.logger.LogWarning("{err}; moving to dead-letter queue...", e.Message);
            await inboundDeadletterQueue.SendMessageAsync(e.QueueBody, cancellationToken);
            await inboundQueue.DeleteMessageAsync(e.QueueMessage.MessageId, e.QueueMessage.PopReceipt, cancellationToken);
            this.logger.LogWarning("successfully moved message {m} to dead-letter queue.", e.QueueMessage.MessageId);
            return false;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error processing message from queue {q}...", inboundQueue.Name);
            return true;
        }
    }

    private async Task DelayAfterDequeue()
    {
        if (this.config.MS_BETWEEN_DEQUEUE_CURRENT < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_BETWEEN_DEQUEUE_CURRENT));
    }

    private async Task DelayAfterEmpty()
    {
        if (this.config.MS_TO_PAUSE_WHEN_EMPTY < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_TO_PAUSE_WHEN_EMPTY));
    }

    private async Task<int> GetMessagesFromInboundQueuesAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        try
        {
            for (int i = 0; i < this.inboundInferenceQueues.Count; i++)
            {
                if (await this.ProcessInferenceRequestAsync(
                    this.inboundInferenceQueues[i],
                    this.inboundInferenceDeadletterQueues[i],
                    cancellationToken))
                {
                    count++;
                    await this.DelayAfterDequeue();
                }
            }
            for (int i = 0; i < this.inboundEvaluationQueues.Count; i++)
            {
                if (await this.ProcessEvaluationRequestAsync(
                    this.inboundEvaluationQueues[i],
                    this.inboundEvaluationDeadletterQueues[i],
                    cancellationToken))
                {
                    count++;
                    await this.DelayAfterDequeue();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore; this is expected when stopping
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error getting messages from queues...");
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("starting to listen for pipeline requests in AzureStorageQueueReader...");
        while (!stoppingToken.IsCancellationRequested)
        {
            var messagesFound = await this.GetMessagesFromInboundQueuesAsync(stoppingToken);
            if (messagesFound == 0)
            {
                await this.DelayAfterEmpty();
            }
        }
    }
}