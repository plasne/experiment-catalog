using Microsoft.AspNetCore.Mvc;

namespace exp_catalog.Controllers;

[ApiController]
[Route("api/projects/{projectName}/experiments/{experimentName}/results")]
public class ResultsController : ControllerBase
{
    private readonly ILogger<ResultsController> logger;

    public ResultsController(ILogger<ResultsController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromBody] Result result)
    {
        if (projectName is null || experimentName is null || result is null)
        {
            return BadRequest("a project name, experiment name, and result (as body) are required.");
        }

        if (result.Ref is null || result.Set is null)
        {
            return BadRequest("ref and set are required.");
        }

        if (result.Metrics is not null && result.Metrics.Any(x => x.Value is null))
        {
            return BadRequest("all metrics must have a value.");
        }

        await storage.AddResult(projectName, experimentName, result);
        return Ok();
    }
}
