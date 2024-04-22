using Microsoft.AspNetCore.Mvc;

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
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName)
    {
        var experiments = await storageService.GetExperiments(projectName);
        return Ok(experiments);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment)
    {
        // TODO: validate projectName and experiment
        await storageService.AddExperiment(projectName, experiment);
        return Ok();
    }

    [HttpPatch("{experimentName}/baseline")]
    public async Task<IActionResult> SetBaseline(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName)
    {
        await storageService.SetExperimentAsBaseline(projectName, experimentName);
        return Ok();
    }

    [HttpGet("{experimentName}/compare")]
    public async Task<ActionResult<Comparison>> Compare(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromQuery] int count = 0)
    {
        var comparison = new Comparison();

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaseline(projectName);
            comparison.LastResultForBaselineExperiment = baseline.AggregateLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperiment(projectName, experimentName);
        comparison.BaselineResultForChosenExperiment =
            experiment.AggregateBaselineSet()
            ?? experiment.AggregateFirstSet();
        comparison.LastResultsForChosenExperiment = experiment.AggregateLastSets(count);

        return Ok(comparison);
    }

    [HttpGet("{experimentName}/sets/{setName}/compare-by-ref")]
    public async Task<ActionResult<ComparisonByRef>> CompareByRef(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromRoute] string setName)
    {
        var comparison = new ComparisonByRef();

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaseline(projectName);
            comparison.LastResultsForBaselineExperiment = baseline.AggregateLastSetByRef();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperiment(projectName, experimentName);
        comparison.BaselineResultsForChosenExperiment =
            experiment.AggregateBaselineSetByRef()
            ?? experiment.AggregateFirstSetByRef();
        comparison.ChosenResultsForChosenExperiment = experiment.AggregateSetByRef(setName);

        return Ok(comparison);
    }

    [HttpGet("{experimentName}/sets/{setName}")]
    public async Task<ActionResult<Comparison>> GetNamedSet(
    [FromServices] IStorageService storageService,
    [FromRoute] string projectName,
    [FromRoute] string experimentName,
    [FromRoute] string setName)
    {
        var experiment = await storageService.GetExperiment(projectName, experimentName);
        var results = experiment.GetAllResultsOfSet(setName);
        return Ok(results);
    }
}
