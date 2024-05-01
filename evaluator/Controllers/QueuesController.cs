using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

[ApiController]
[Route("api/queues")]
public class QueuesController(IConfig config, IHttpClientFactory httpClientFactory, ILogger<QueuesController> logger) : ControllerBase
{
    private readonly IConfig config = config;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ILogger<QueuesController> logger = logger;

    [HttpGet]
    public async Task<ActionResult<List<Queue>>> List(
        [FromServices] IQueueService queueService,
        CancellationToken cancellationToken)
    {
        var queues = await queueService.ListQueues(cancellationToken);
        return Ok(queues);
    }

    [HttpPost("{queueName}")]
    public async Task<ActionResult<EnqueueResponse>> Enqueue(
        [FromServices] IQueueService queueService,
        [FromServices] IBlobStorageService storageService,
        [FromRoute] string queueName,
        [FromBody] EnqueueRequest request,
        CancellationToken cancellationToken)
    {
        // validate
        if (string.IsNullOrEmpty(request.Project) || string.IsNullOrEmpty(request.Experiment) || string.IsNullOrEmpty(request.Set))
        {
            return BadRequest("project, experiment, and set are required.");
        }

        // init
        var successful = new ConcurrentBag<string>();
        var failed = new ConcurrentBag<string>();
        var httpClient = this.httpClientFactory.CreateClient();
        var semaphore = new SemaphoreSlim(this.config.CONCURRENCY);
        var prefix = Guid.NewGuid().ToString();
        var yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

        // get the ground truth URIs
        this.logger.LogDebug("getting ground truth URIs...");
        var groundTruthUris = await storageService.ListGroundTruthUris(cancellationToken);
        this.logger.LogInformation("obtained {c} ground truth URIs.", groundTruthUris.Count);

        // open them to get the refs
        var tasks = groundTruthUris.Select(async groundTruthUri =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // download the ground truth
                this.logger.LogDebug("downloading {uri}...", groundTruthUri);
                var response = await httpClient.GetAsync(groundTruthUri);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"{response.StatusCode}: {responseBody}");
                }
                this.logger.LogInformation("successfully downloaded {uri}...", groundTruthUri);

                // get the ground truth ref
                GroundTruthFile? payload;
                var filepath = groundTruthUri.Split("?").First();
                if (filepath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                {
                    payload = JsonSerializer.Deserialize<GroundTruthFile>(responseBody);
                    if (string.IsNullOrEmpty(payload?.Ref))
                    {
                        throw new Exception($"no ref found.");
                    }
                }
                else if (filepath.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase))
                {
                    payload = yamlDeserializer.Deserialize<GroundTruthFile>(responseBody);
                    if (string.IsNullOrEmpty(payload?.Ref))
                    {
                        throw new Exception($"no ref found.");
                    }
                }
                else
                {
                    throw new Exception($"cannot determine ground truth file type for {groundTruthUri}.");
                }

                // create the inference blob
                this.logger.LogDebug("creating inference blob...");
                var inferenceBlobUri = await storageService.CreateInferenceBlob($"{prefix}-{payload.Ref}.json", cancellationToken);
                this.logger.LogInformation("created inference blob as {uri}.", inferenceBlobUri);

                // create the evaluation blob
                this.logger.LogDebug("creating evaluation blob...");
                var evaluationBlobUri = await storageService.CreateEvaluationBlob($"{prefix}-{payload.Ref}.json", cancellationToken);
                this.logger.LogInformation("created evaluation blob as {uri}.", evaluationBlobUri);

                // create the payload
                var inferenceQueue = $"{queueName}-inference";
                this.logger.LogDebug(
                    "enqueuing (ref:{r}, set:{s}) as an inference request to queue {q}...",
                    payload.Ref,
                    request.Set,
                    inferenceQueue);
                var inferenceRequest = new PipelineRequest
                {
                    Project = request.Project,
                    Experiment = request.Experiment,
                    Ref = payload.Ref,
                    Set = request.Set,
                    IsBaseline = request.IsBaseline,
                    GroundTruthUri = groundTruthUri,
                    InferenceUri = inferenceBlobUri,
                    EvaluationUri = evaluationBlobUri,
                };
                var json = JsonSerializer.Serialize(inferenceRequest);

                // enqueue the payload the appropriate number of times
                for (var i = 0; i < request.Iterations; i++)
                {
                    await queueService.Enqueue(inferenceQueue, json, cancellationToken);
                    this.logger.LogInformation(
                        "successfully enqueued (ref:{r}, set:{s}) as an inference request to queue {q} as iteration {i}.",
                        payload.Ref,
                        request.Set,
                        inferenceQueue,
                        i);
                }

                successful.Add(groundTruthUri);
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "failed to enqueue {uri}...", groundTruthUri);
                failed.Add(groundTruthUri);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);

        return Ok(new EnqueueResponse
        {
            Successful = new List<string>(successful),
            Failed = new List<string>(failed),
        });
    }
}
