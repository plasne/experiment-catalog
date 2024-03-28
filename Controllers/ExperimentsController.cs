using Microsoft.AspNetCore.Mvc;

namespace exp_catalog.Controllers;

[ApiController]
[Route("api/projects/{projectName}/experiments")]
public class ExperimentsController : ControllerBase
{
    private readonly ILogger<ExperimentsController> logger;

    public ExperimentsController(ILogger<ExperimentsController> logger)
    {
        this.logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Experiment>>> List(
        [FromServices] IStorage storage,
        [FromRoute] string projectName)
    {
        try
        {
            var experiments = await storage.GetExperiments(projectName);
            return Ok(experiments);
        }
        catch (HttpException e)
        {
            logger.LogWarning(e, "Failed to list experiments for project {projectName}.", projectName);
            return StatusCode(e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to list experiments for project {projectName}.", projectName);
            return StatusCode(500, e.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment)
    {
        // TODO: validate projectName and experiment
        try
        {
            await storage.AddExperiment(projectName, experiment);
            return Ok();
        }
        catch (HttpException e)
        {
            logger.LogWarning(e, "Failed to add experiment {experiment.Name} to project {projectName}.", experiment.Name, projectName);
            return StatusCode(e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add experiment {experiment.Name} to project {projectName}.", experiment.Name, projectName);
            return StatusCode(500, e.Message);
        }
    }

    [HttpPatch("{experimentName}/baseline")]
    public async Task<IActionResult> SetBaseline(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName)
    {
        try
        {
            await storage.SetExperimentAsBaseline(projectName, experimentName);
            return Ok();
        }
        catch (HttpException e)
        {
            logger.LogWarning(e, "Failed to set experiment {experiment.Name} as baseline for project {projectName}.", experimentName, projectName);
            return StatusCode(e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set experiment {experiment.Name} as baseline for project {projectName}.", experimentName, projectName);
            return StatusCode(500, e.Message);
        }
    }

    [HttpGet("{experimentName}/compare")]
    public async Task<ActionResult<Comparison>> Compare(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName)
    {
        var comparison = new Comparison();

        // get the baseline
        try
        {
            var baseline = await storage.GetProjectBaseline(projectName);
            comparison.LastResultForBaselineExperiment = baseline.GetLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the chosen experiment
        try
        {
            var experiment = await storage.GetExperiment(projectName, experimentName);
            comparison.BaselineResultForChosenExperiment =
                experiment.GetBaselineSet()
                ?? experiment.GetFirstSet();
            comparison.LastResultForChosenExperiment = experiment.GetLastSet();
        }
        catch (HttpException e)
        {
            logger.LogWarning(e, "Failed to compare experiment {experiment.Name} in project {projectName}.", experimentName, projectName);
            return StatusCode(e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to compare experiment {experiment.Name} in project {projectName}.", experimentName, projectName);
            return StatusCode(500, e.Message);
        }

        return Ok(comparison);
    }
}
