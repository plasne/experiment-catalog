using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
    private readonly List<QueueClient> inboundEvaluationQueues = [];
    private QueueClient? outboundInferenceQueue;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // try and connect to all the inbound inference queues
        foreach (var queue in this.config.INBOUND_INFERENCE_QUEUES)
        {
            var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundInferenceQueues.Add(queueClient);
        }

        // try and connect to all the inbound evaluation queues
        foreach (var queue in this.config.INBOUND_EVALUATION_QUEUES)
        {
            var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
            await queueClient.ConnectAsync(this.logger, cancellationToken);
            this.inboundEvaluationQueues.Add(queueClient);
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

    private async Task UploadBlob(string containerName, string blobName, string content, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to upload {c}/{b}...", containerName, blobName);
        var blobClient = this.GetBlobClient(containerName, blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var response = await blobClient.UploadAsync(stream, cancellationToken);
        this.logger.LogInformation("successfully uploaded {c}/{b}.", containerName, blobName);
    }

    private async Task<string> SendForProcessing(string url, string content, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to call '{u}' for processing...", url);
        using var httpClient = this.httpClientFactory.CreateClient("retry");
        var requestBody = new StringContent(content, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(this.config.INFERENCE_URL, requestBody, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"calling {url} resulted in {response.StatusCode}: {responseBody}.");
        }
        if (string.IsNullOrEmpty(responseBody))
        {
            throw new Exception("response body is empty.");
        }
        this.logger.LogInformation("successfully called '{u}' for processing.", url);
        return responseBody;
    }

    private async Task<bool> ProcessInferenceRequest(QueueClient inboundQueue, CancellationToken cancellationToken)
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

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");

            // download and transform the ground truth file
            var groundTruthBlobRef = new BlobRef(request.GroundTruthUri);
            var groundTruthBlobClient = this.GetBlobClient(groundTruthBlobRef.Container, groundTruthBlobRef.BlobName);
            var groundTruthContent = await groundTruthBlobClient.DownloadAndTransform(
                this.config.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // call processing URL
            var responseContent = await this.SendForProcessing(this.config.INFERENCE_URL, groundTruthContent, cancellationToken);

            // upload the result
            await this.UploadBlob(this.config.INFERENCE_CONTAINER, request.Id + ".json", responseContent, cancellationToken);

            // enqueue for the next stage
            await this.outboundInferenceQueue!.SendMessageAsync(body, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error processing message from queue {q}...", inboundQueue.Name);
            return false;
        }
    }

    private async Task<bool> ProcessEvaluationRequest(QueueClient inboundQueue, CancellationToken cancellationToken)
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

            // deserialize the pipeline request
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");

            // download and transform the inference file
            var inferenceBlobClient = this.GetBlobClient(this.config.INFERENCE_CONTAINER, request.Id + ".json");
            var inferenceContent = await inferenceBlobClient.DownloadAndTransform(
                this.config.INBOUND_INFERENCE_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);

            // call processing URL
            var responseContent = await this.SendForProcessing(this.config.EVALUATION_URL, inferenceContent, cancellationToken);

            // upload the result
            await this.UploadBlob(this.config.EVALUATION_CONTAINER, request.Id + ".json", responseContent, cancellationToken);

            // delete the message
            await inboundQueue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error processing message from queue {q}...", inboundQueue.Name);
            return false;
        }
    }

    private async Task<int> GetMessagesFromInboundQueues(CancellationToken cancellationToken)
    {
        var count = 0;
        try
        {
            foreach (var queue in this.inboundInferenceQueues)
            {
                if (await this.ProcessInferenceRequest(queue, cancellationToken))
                {
                    count++;
                }
            }
            foreach (var queue in this.inboundEvaluationQueues)
            {
                if (await this.ProcessEvaluationRequest(queue, cancellationToken))
                {
                    count++;
                }
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error getting messages from queues...");
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // look for messages in the inbound queues
        while (!stoppingToken.IsCancellationRequested)
        {
            var messagesFound = await this.GetMessagesFromInboundQueues(stoppingToken);
            if (messagesFound == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_TO_PAUSE_WHEN_EMPTY), stoppingToken);
            }
        }
    }
}