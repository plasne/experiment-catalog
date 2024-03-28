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
        // TODO: validate projectName and experimentName
        // TODO: validate result
        try
        {
            await storage.AddResult(projectName, experimentName, result);
            return Ok();
        }
        catch (HttpException e)
        {
            logger.LogWarning(e, "Failed to add result to experiment {experiment.Name} to project {projectName}.", experimentName, projectName);
            return StatusCode(e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add result to experiment {experiment.Name} to project {projectName}.", experimentName, projectName);
            return StatusCode(500, e.Message);
        }
    }
}
