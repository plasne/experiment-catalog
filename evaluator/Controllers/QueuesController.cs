using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/queues")]
public class QueuesController : ControllerBase
{
    private readonly ILogger<QueuesController> logger;

    public QueuesController(ILogger<QueuesController> logger)
    {
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

    [HttpPost("{queueName}/enqueue")]
    public async Task<ActionResult<List<string>>> Enqueue(
        [FromServices] IQueueService queueService,
        [FromServices] IStorageService storageService,
        [FromRoute] string queueName,
        CancellationToken cancellationToken)
    {
        var groundTruthUris = await storageService.ListGroundTruthUris(cancellationToken);
        return Ok(groundTruthUris);
    }
}
