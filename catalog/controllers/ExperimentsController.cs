using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetBricks;

namespace Catalog;

[ApiController]
[Route("api/projects/{projectName}/experiments")]
public class ExperimentsController(ILogger<ExperimentsController> logger) : ControllerBase
{
    private readonly ILogger<ExperimentsController> logger = logger;

    [HttpGet]
    public async Task<ActionResult<IList<Experiment>>> List(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        CancellationToken cancellationToken)
    {
        var experiments = await storageService.GetExperimentsAsync(projectName, cancellationToken);
        return Ok(experiments);
    }

    [HttpGet("{experimentName}")]
    public async Task<ActionResult<Experiment>> Get(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken)
    {
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken);
        return Ok(experiment);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment,
        CancellationToken cancellationToken)
    {
        if (projectName is null || experiment is null)
        {
            return BadRequest("a project name and experiment (as body) are required.");
        }

        if (string.IsNullOrEmpty(experiment.Name) || string.IsNullOrEmpty(experiment.Hypothesis))
        {
            return BadRequest("an experiment name and hypothesis are required.");
        }

        await storageService.AddExperimentAsync(projectName, experiment, cancellationToken);
        return Ok();
    }

    [HttpPatch("{experimentName}/baseline")]
    public async Task<IActionResult> SetBaseline(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken)
    {
        await storageService.SetExperimentAsBaselineAsync(projectName, experimentName, cancellationToken);
        return Ok();
    }

    private static async Task<(IList<Tag> includeTags, IList<Tag> excludeTags)> LoadTags(
        IStorageService storageService,
        string projectName,
        string includeTagsStr,
        string excludeTagsStr,
        CancellationToken cancellationToken)
    {
        var includeTags = await storageService.GetTagsAsync(projectName, includeTagsStr.AsArray(() => []), cancellationToken);
        var excludeTags = await storageService.GetTagsAsync(projectName, excludeTagsStr.AsArray(() => []), cancellationToken);
        return (includeTags, excludeTags);
    }

    [HttpGet("{experimentName}/compare")]
    public async Task<ActionResult<Comparison>> Compare(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken,
        [FromQuery] int count = 0,
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        var comparison = new Comparison();
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            baseline.Filter(includeTags, excludeTags);
            comparison.LastResultForBaselineExperiment = baseline.AggregateLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken);
        experiment.Filter(includeTags, excludeTags);
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
        CancellationToken cancellationToken,
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        var comparison = new ComparisonByRef();
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            baseline.Filter(includeTags, excludeTags);
            comparison.LastResultsForBaselineExperiment = baseline.AggregateLastSetByRef();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken);
        experiment.Filter(includeTags, excludeTags);
        comparison.BaselineResultsForChosenExperiment =
            experiment.AggregateBaselineSetByRef()
            ?? experiment.AggregateFirstSetByRef();
        comparison.ChosenResultsForChosenExperiment = experiment.AggregateSetByRef(setName);

        // run policies
        if (comparison.ChosenResultsForChosenExperiment is not null
            && comparison.BaselineResultsForChosenExperiment is not null)
        {
            var definitions = new Dictionary<string, MetricDefinition>
            {
                { "ndcg", new MetricDefinition { Min = 0, Max = 1 } },
                { "bertscore", new MetricDefinition { Min = 0, Max = 1 } },
                { "groundedness", new MetricDefinition { Min = 1, Max = 5 } }
            };
            var policy = new PercentImprovement();
            foreach (var (key, result) in comparison.ChosenResultsForChosenExperiment)
            {
                if (comparison.BaselineResultsForChosenExperiment.TryGetValue(key, out var baseline))
                {
                    policy.Evaluate(result, baseline, definitions);
                }
            }
            this.logger.LogWarning("policy passed? {0}, {1}, {2}", policy.IsPassed, policy.NumResultsThatPassed, policy.NumResultsThatFailed);
            this.logger.LogWarning(policy.Requirement);
            this.logger.LogWarning(policy.Actual);
        }

        return Ok(comparison);
    }

    [HttpGet("{experimentName}/sets/{setName}")]
    public async Task<ActionResult<Comparison>> GetNamedSet(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromRoute] string setName,
        CancellationToken cancellationToken,
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken);
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        experiment.Filter(includeTags, excludeTags);
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
        await storageService.OptimizeExperimentAsync(projectName, experimentName, cancellationToken);
        return Ok();
    }
}
