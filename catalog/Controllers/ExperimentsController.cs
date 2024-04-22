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
        [FromRoute] string projectName,
        CancellationToken cancellationToken)
    {
        var experiments = await storageService.GetExperiments(projectName, cancellationToken);
        return Ok(experiments);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment,
        CancellationToken cancellationToken)
    {
        // TODO: validate projectName and experiment
        await storageService.AddExperiment(projectName, experiment, cancellationToken);
        return Ok();
    }

    [HttpPatch("{experimentName}/baseline")]
    public async Task<IActionResult> SetBaseline(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken)
    {
        await storageService.SetExperimentAsBaseline(projectName, experimentName, cancellationToken);
        return Ok();
    }

    [HttpGet("{experimentName}/compare")]
    public async Task<ActionResult<Comparison>> Compare(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken,
        [FromQuery] int count = 0)
    {
        var comparison = new Comparison();

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaseline(projectName, cancellationToken);
            comparison.LastResultForBaselineExperiment = baseline.AggregateLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperiment(projectName, experimentName, cancellationToken);
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
        [FromRoute] string setName,
        CancellationToken cancellationToken)
    {
        var comparison = new ComparisonByRef();

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaseline(projectName, cancellationToken);
            comparison.LastResultsForBaselineExperiment = baseline.AggregateLastSetByRef();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperiment(projectName, experimentName, cancellationToken);
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
    [FromRoute] string setName,
    CancellationToken cancellationToken)
    {
        var experiment = await storageService.GetExperiment(projectName, experimentName, cancellationToken);
        var results = experiment.GetAllResultsOfSet(setName);
        return Ok(results);
    }
}
