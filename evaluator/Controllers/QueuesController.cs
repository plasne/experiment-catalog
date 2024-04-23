using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/queues")]
public class QueuesController : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<QueuesController> logger;

    public QueuesController(IHttpClientFactory httpClientFactory, ILogger<QueuesController> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

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
        [FromServices] IStorageService storageService,
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
        var semaphore = new SemaphoreSlim(4);
        var prefix = Guid.NewGuid().ToString();

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
                this.logger.LogInformation("successfullly downloaded {uri}...", groundTruthUri);

                // get the ground truth ref
                var payload = JsonSerializer.Deserialize<GroundTruthFile>(responseBody);
                if (string.IsNullOrEmpty(payload?.Ref))
                {
                    throw new Exception($"no ref found.");
                }

                // create the inference blob
                this.logger.LogDebug("creating inference blob...");
                var inferenceBlobUri = await storageService.CreateInferenceBlob($"{prefix}-{payload.Ref}.json", cancellationToken);
                this.logger.LogInformation("created inference blob as {uri}.", inferenceBlobUri);

                // create the evaluation blob
                this.logger.LogDebug("creating evaluation blob...");
                var evaluationBlobUri = await storageService.CreateEvaluationBlob($"{prefix}-{payload.Ref}.json", cancellationToken);
                this.logger.LogInformation("created evaluation blob as {uri}.", evaluationBlobUri);

                // enqueue the ref
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
                await queueService.Enqueue(inferenceQueue, json, cancellationToken);
                this.logger.LogDebug(
                    "successfully enqueued (ref:{r}, set:{s}) as an inference request to queue {q}.",
                    payload.Ref,
                    request.Set,
                    inferenceQueue);

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
            Successful = [.. successful],
            Failed = [.. failed],
        });
    }
}
