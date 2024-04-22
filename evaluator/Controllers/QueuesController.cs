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
    public async Task<ActionResult<List<Queue>>> List([FromServices] IQueueService queueService)
    {
        var queues = await queueService.ListQueues();
        return Ok(queues);
    }

    [HttpPost("{queueName}/enqueue")]
    public async Task<ActionResult<List<string>>> Enqueue(
        [FromServices] IQueueService queueService,
        [FromServices] IStorageService storageService,
        [FromRoute] string queueName)
    {
        var groundTruthUris = await storageService.ListGroundTruthUris();
        return Ok(groundTruthUris);
    }
}
