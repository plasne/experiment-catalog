using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/projects/{projectName}/experiments/{experimentName}/results")]
public class ResultsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromBody] Result result)
    {
        if (projectName is null || experimentName is null || result is null)
        {
            return BadRequest("a project name, experiment name, and result (as body) are required.");
        }

        if (result.Ref is null || result.Set is null || result.Metrics is null)
        {
            return BadRequest("ref, set, and metrics are required.");
        }

        if (result.Metrics.Any(x => x.Value is null))
        {
            return BadRequest("all metrics must have a value.");
        }

        await storageService.AddResult(projectName, experimentName, result);
        return Ok();
    }
}
