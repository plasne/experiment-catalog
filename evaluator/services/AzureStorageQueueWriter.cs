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
using NetBricks;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueWriter(
    IConfigFactory<IConfig> configFactory,
    DefaultAzureCredential defaultAzureCredential,
    JobStatusService jobStatusService,
    ILogger<AzureStorageQueueWriter> logger)
    : BackgroundService
{
    private readonly IConfigFactory<IConfig> configFactory = configFactory;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly JobStatusService jobStatusService = jobStatusService;
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
            // get configuration
            var config = await this.configFactory.GetAsync(cancellationToken);

            // load the blob file
            var blobClient = containerClient.GetBlobClient(blob.Name);
            string content = await blobClient.DownloadAndTransformAsync(
                config.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY,
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
                    Id = groundTruthFile.Ref + ":" + i,
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

    private async Task<QueueClient> GetQueueClientAsync(string queue, CancellationToken cancellationToken)
    {
        var config = await this.configFactory.GetAsync(cancellationToken);
        var queueUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
        var queueClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
            ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
            : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, queue);
        return queueClient;
    }

    private async Task<BlobContainerClient> GetBlobContainerClientAsync(string container, CancellationToken cancellationToken)
    {
        var config = await this.configFactory.GetAsync(cancellationToken);
        var blobUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        var blobClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
            ? new BlobServiceClient(new Uri(blobUrl), this.defaultAzureCredential)
            : new BlobServiceClient(config.AZURE_STORAGE_CONNECTION_STRING);
        return blobClient.GetBlobContainerClient(container);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = await configFactory.GetAsync(stoppingToken);
        if (!config.ROLES.Contains(Roles.API))
        {
            return;
        }
        this.logger.LogInformation("starting to listen for enqueue requests in AzureStorageQueueWriter...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enqueueRequest = await this.enqueueRequests.Reader.ReadAsync(stoppingToken);

                // try and connect to the output queue
                var queueClient = await this.GetQueueClientAsync(enqueueRequest.Queue, stoppingToken);
                await queueClient.ConnectAsync(this.logger, stoppingToken);

                // enqueue everything from each specified container
                var totalItems = 0;
                foreach (var containerPlusPath in enqueueRequest.Containers)
                {
                    // connect to the blob container
                    var containerAndPath = containerPlusPath.Split('/', 2);
                    var containerClient = await this.GetBlobContainerClientAsync(containerAndPath[0], stoppingToken);

                    // enqueue blobs from that container
                    var prefix = containerAndPath.Length == 2 ? containerAndPath[1] : null;
                    await foreach (var blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: prefix, cancellationToken: stoppingToken))
                    {
                        await this.EnqueueBlobAsync(containerClient, blob, enqueueRequest, queueClient, stoppingToken);
                        totalItems += enqueueRequest.Iterations;
                    }
                }

                // create the job status blob with the final total item count
                await this.jobStatusService.CreateJobAsync(
                    enqueueRequest.RunId,
                    enqueueRequest.Project,
                    enqueueRequest.Experiment,
                    enqueueRequest.Set,
                    totalItems,
                    stoppingToken);
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