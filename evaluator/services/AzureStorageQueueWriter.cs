using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class AzureStorageQueueWriter(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    ILogger<AzureStorageQueueWriter> logger)
    : BackgroundService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<AzureStorageQueueWriter> logger = logger;
    private readonly BlockingCollection<EnqueueRequest> enqueueRequests = [];
    private BlobContainerClient? containerClient;
    private QueueClient? outboundQueue;

    public void StartEnqueueRequest(EnqueueRequest req)
    {
        this.enqueueRequests.Add(req);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // try and get to the blob container
        var uri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        var blobClient = new BlobServiceClient(new Uri(uri), this.defaultAzureCredential);
        // TODO: containers come from datasources
        this.containerClient = blobClient.GetBlobContainerClient();
        await containerClient.ExistsAsync(cancellationToken);

        // try and connect to the outbound queue
        var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{this.config.OUTBOUND_GROUNDTRUTH_QUEUE}";
        var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
        await queueClient.ConnectAsync(this.logger, cancellationToken);
        this.outboundQueue = queueClient;

        await base.StartAsync(cancellationToken);
    }

    private async Task EnqueueBlob(BlobItem blob, EnqueueRequest enqueueRequest, CancellationToken cancellationToken)
    {
        try
        {
            // load the blob file
            var blobClient = this.containerClient!.GetBlobClient(blob.Name);
            string content = await blobClient.DownloadAndTransform(
                this.config.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);
            var groundTruthFile = JsonConvert.DeserializeObject<GroundTruthFile>(content)
                ?? throw new Exception($"failed to deserialize to GroundTruthFile.");

            // build the pipeline request
            var pipelineRequest = new PipelineRequest
            {
                Id = Guid.NewGuid().ToString(),
                GroundTruthUri = this.containerClient.Name + "/" + blob.Name,
                Project = enqueueRequest.Project,
                Experiment = enqueueRequest.Experiment,
                Ref = groundTruthFile.Ref,
                Set = enqueueRequest.Set,
                IsBaseline = enqueueRequest.IsBaseline,
            };

            // enqueue in blob
            await this.outboundQueue!.SendMessageAsync(JsonConvert.SerializeObject(pipelineRequest), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // ignore; this is expected when stopping
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "error when trying to enqueue ground truth file {b}...", blob.Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enqueueRequest = this.enqueueRequests.Take(stoppingToken);
                await foreach (var blob in this.containerClient!.GetBlobsAsync(cancellationToken: stoppingToken))
                {
                    await this.EnqueueBlob(blob, enqueueRequest, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore; this is expected when stopping
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "error processing an enqueue request...");
            }
        }
    }
}