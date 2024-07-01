using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueWriter(
    IConfig config,
    ILogger<AzureStorageQueueWriter> logger,
    DefaultAzureCredential? defaultAzureCredential = null)
    : BackgroundService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential? defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<AzureStorageQueueWriter> logger = logger;
    private readonly Channel<EnqueueRequest> enqueueRequests = Channel.CreateUnbounded<EnqueueRequest>();

    public ValueTask StartEnqueueRequestAsync(EnqueueRequest req)
    {
        // add RunId if there isn't one
        if (req.RunId == Guid.Empty)
        {
            req.RunId = Guid.NewGuid();
        }

        // enqueue
        return this.enqueueRequests.Writer.WriteAsync(req);
    }

    private async Task EnqueueBlobAsync(
        BlobContainerClient containerClient,
        BlobItem blob,
        EnqueueRequest enqueueRequest,
        QueueClient queueClient,
        CancellationToken cancellationToken)
    {
        try
        {
            // load the blob file
            var blobClient = containerClient.GetBlobClient(blob.Name);
            string content = await blobClient.DownloadAndTransformAsync(
                this.config.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY,
                this.logger,
                cancellationToken);
            var groundTruthFile = JsonConvert.DeserializeObject<GroundTruthFile>(content)
                ?? throw new Exception($"failed to deserialize to GroundTruthFile.");

            // handle multiple iterations
            for (int i = 0; i < enqueueRequest.Iterations; i++)
            {
                using var activity = DiagnosticService.Source.StartActivity("enqueue-evaluation-ref", ActivityKind.Internal);

                // build the pipeline request
                var pipelineRequest = new PipelineRequest
                {
                    RunId = enqueueRequest.RunId,
                    Id = activity?.Id ?? Guid.NewGuid().ToString(),
                    GroundTruthUri = containerClient.Name + "/" + blob.Name,
                    Project = enqueueRequest.Project,
                    Experiment = enqueueRequest.Experiment,
                    Ref = groundTruthFile.Ref,
                    Set = enqueueRequest.Set,
                    IsBaseline = enqueueRequest.IsBaseline,
                    InferenceHeaders = enqueueRequest.InferenceHeaders,
                    EvaluationHeaders = enqueueRequest.EvaluationHeaders,
                };

                // enqueue in blob
                activity?.AddTagsFromPipelineRequest(pipelineRequest);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await queueClient.SendMessageAsync(JsonConvert.SerializeObject(pipelineRequest), timeout.Token);
            }
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

    private QueueClient GetQueueClient(string queue)
    {
        var queueUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
        var queueClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
            ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
            : new QueueClient(this.config.AZURE_STORAGE_CONNECTION_STRING, queue);
        return queueClient;
    }

    private BlobContainerClient GetBlobContainerClient(string container)
    {
        var blobUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        var blobClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
            ? new BlobServiceClient(new Uri(blobUrl), this.defaultAzureCredential)
            : new BlobServiceClient(this.config.AZURE_STORAGE_CONNECTION_STRING);
        return blobClient.GetBlobContainerClient(container);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("starting to listen for enqueue requests in AzureStorageQueueWriter...");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enqueueRequest = await this.enqueueRequests.Reader.ReadAsync(stoppingToken);

                // try and connect to the output queue
                var queueClient = this.GetQueueClient(enqueueRequest.Queue);
                await queueClient.ConnectAsync(this.logger, stoppingToken);

                // enqueue everything from each specified container
                foreach (var containerPlusPath in enqueueRequest.Containers)
                {
                    // connect to the blob container
                    var containerAndPath = containerPlusPath.Split('/', 2);
                    var containerClient = this.GetBlobContainerClient(containerAndPath[0]);

                    // enqueue blobs from that container
                    var prefix = containerAndPath.Length == 2 ? containerAndPath[1] : null;
                    await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: stoppingToken))
                    {
                        await this.EnqueueBlobAsync(containerClient, blob, enqueueRequest, queueClient, stoppingToken);
                    }
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