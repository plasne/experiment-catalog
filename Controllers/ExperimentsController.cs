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
        var experiments = await storage.GetExperiments(projectName);
        return Ok(experiments);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment)
    {
        // TODO: validate projectName and experiment
        await storage.AddExperiment(projectName, experiment);
        return Ok();
    }

    [HttpPatch("{experimentName}/baseline")]
    public async Task<IActionResult> SetBaseline(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName)
    {
        await storage.SetExperimentAsBaseline(projectName, experimentName);
        return Ok();
    }

    [HttpGet("{experimentName}/compare")]
    public async Task<ActionResult<Comparison>> Compare(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromQuery] int count = 0)
    {
        var comparison = new Comparison();

        // get the baseline
        try
        {
            var baseline = await storage.GetProjectBaseline(projectName);
            comparison.LastResultForBaselineExperiment = baseline.AggregateLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storage.GetExperiment(projectName, experimentName);
        comparison.BaselineResultForChosenExperiment =
            experiment.AggregateBaselineSet()
            ?? experiment.AggregateFirstSet();
        comparison.LastResultsForChosenExperiment = experiment.AggregateLastSets(count);

        return Ok(comparison);
    }

    [HttpGet("{experimentName}/compare-by-ref")]
    public async Task<ActionResult<ComparisonByRef>> CompareByRef(
        [FromServices] IStorage storage,
        [FromRoute] string projectName,
        [FromRoute] string experimentName)
    {
        return Ok(new ComparisonByRef());
    }

    [HttpGet("{experimentName}/sets/{setName}")]
    public async Task<ActionResult<Comparison>> GetNamedSet(
    [FromServices] IStorage storage,
    [FromRoute] string projectName,
    [FromRoute] string experimentName,
    [FromRoute] string setName)
    {
        var experiment = await storage.GetExperiment(projectName, experimentName);
        var results = experiment.GetAllResultsOfSet(setName);
        return Ok(results);
    }
}
