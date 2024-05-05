using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ILogger<ProjectsController> logger;

    public ProjectsController(ILogger<ProjectsController> logger)
    {
        this.logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> List(
        [FromServices] IStorageService storageService,
        CancellationToken cancellationToken)
    {
        var projects = await storageService.GetProjects(cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromBody] Project project,
        CancellationToken cancellationToken)
    {
        if (project is null)
        {
            return BadRequest("a project (as body) is required.");
        }

        if (string.IsNullOrEmpty(project.Name))
        {
            return BadRequest("an project name is required.");
        }

        await storageService.AddProject(project, cancellationToken);
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
        Stopwatch stopwatch = new();
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

    [HttpPut("{experimentName}/optimize")]
    public async Task<IActionResult> Optimize(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken)
    {
        await storageService.OptimizeExperiment(projectName, experimentName, cancellationToken);
        return Ok();
    }
}
