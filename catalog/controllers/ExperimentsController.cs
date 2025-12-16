using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
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
        if (string.IsNullOrEmpty(projectName))
        {
            return BadRequest("a project name is required.");
        }

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
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(experimentName))
        {
            return BadRequest("a project name and experiment name are required.");
        }

        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, false, cancellationToken);
        return Ok(experiment);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromBody] Experiment experiment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectName) || experiment is null)
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
    public async Task<IActionResult> SetExperimentAsBaseline(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(experimentName))
        {
            return BadRequest("a project name and experiment name are required.");
        }

        await storageService.SetExperimentAsBaselineAsync(projectName, experimentName, cancellationToken);
        return Ok();
    }

    [HttpPatch("{experimentName}/sets/{setName}/baseline")]
    public async Task<IActionResult> SetBaselineForExperiment(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromRoute] string setName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(experimentName) || string.IsNullOrEmpty(setName))
        {
            return BadRequest("a project name, experiment name, and set name are required.");
        }

        await storageService.SetBaselineForExperiment(projectName, experimentName, setName, cancellationToken);
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
        [FromServices] IConfig config,
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        CancellationToken cancellationToken,
        [FromQuery(Name = "sets")] string sets = "",
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(experimentName))
        {
            return BadRequest("a project name and experiment name are required.");
        }

        // init
        var watch = Stopwatch.StartNew();
        var comparison = new Comparison();
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        comparison.MetricDefinitions = (await storageService.GetMetricsAsync(projectName, cancellationToken))
            .ToDictionary(x => x.Name);
        logger.LogDebug("loaded tags and metric definitions in {ms} ms.", watch.ElapsedMilliseconds);

        // get the project baseline
        try
        {
            watch.Restart();
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            var baselineSet = baseline.BaselineSet ?? baseline.LastSet;
            var baselineFiltered = baseline.Filter(includeTags, excludeTags);
            baseline.MetricDefinitions = comparison.MetricDefinitions;
            comparison.ProjectBaseline = new ComparisonEntity
            {
                Project = projectName,
                Experiment = baseline.Name,
                Set = baselineSet,
                Result = baseline.AggregateSet(baselineSet, baselineFiltered),
                Count = baseline.Results?.Count(x => x.Set == baselineSet), // unfiltered count
            };
            logger.LogDebug("loaded project baseline in {ms} ms.", watch.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the experiment baseline
        watch.Restart();
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        var experimentBaselineSet = experiment.BaselineSet ?? experiment.FirstSet;
        var experimentFiltered = experiment.Filter(includeTags, excludeTags);
        experiment.MetricDefinitions = comparison.MetricDefinitions;
        comparison.ExperimentBaseline =
            string.Equals(experiment.Baseline, ":project", StringComparison.OrdinalIgnoreCase)
            ? comparison.ProjectBaseline
            : new ComparisonEntity
            {
                Project = projectName,
                Experiment = experiment.Name,
                Set = experimentBaselineSet,
                Result = experiment.AggregateSet(experimentBaselineSet, experimentFiltered),
                Count = experiment.Results?.Count(x => x.Set == experimentBaselineSet), // unfiltered count
            };
        logger.LogDebug("loaded experiment baseline in {ms} ms.", watch.ElapsedMilliseconds);

        // get the sets
        watch.Restart();
        comparison.Sets = experiment.AggregateAllSets(experimentFiltered)
            .Select(x =>
            {
                // find matching statistics
                var statistics = experiment.Statistics?.LastOrDefault(y =>
                {
                    if (y.Set != x.Set) return false;
                    if (y.BaselineExperiment != comparison.ExperimentBaseline?.Experiment) return false;
                    if (y.BaselineSet != comparison.ExperimentBaseline?.Set) return false;
                    if (y.BaselineResultCount != comparison.ExperimentBaseline?.Count) return false;
                    if (y.SetResultCount != experiment.Results?.Count(z => z.Set == x.Set)) return false; // unfiltered count
                    if (y.NumSamples != config.CALC_PVALUES_USING_X_SAMPLES) return false;
                    if (y.ConfidenceLevel != config.CONFIDENCE_LEVEL) return false;
                    return true;
                });

                // fold statistics into result metrics
                if (statistics?.Metrics is not null && x.Metrics is not null)
                {
                    foreach (var (metricName, statisticsMetric) in statistics.Metrics)
                    {
                        if (x.Metrics.TryGetValue(metricName, out var resultMetric))
                        {
                            resultMetric.PValue = statisticsMetric.PValue;
                            resultMetric.CILower = statisticsMetric.CILower;
                            resultMetric.CIUpper = statisticsMetric.CIUpper;
                        }
                    }
                }

                return new ComparisonEntity
                {
                    Project = projectName,
                    Experiment = experiment.Name,
                    Set = x.Set,
                    Result = x,
                };
            });
        logger.LogDebug("aggregated sets in {ms} ms.", watch.ElapsedMilliseconds);
        watch.Stop();

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
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(experimentName) || string.IsNullOrEmpty(setName))
        {
            return BadRequest("a project name, experiment name, and set name are required.");
        }

        // init
        var comparison = new ComparisonByRef();
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        comparison.MetricDefinitions = (await storageService.GetMetricsAsync(projectName, cancellationToken))
            .ToDictionary(x => x.Name);

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            var baselineFiltered = baseline.Filter(includeTags, excludeTags);
            baseline.MetricDefinitions = comparison.MetricDefinitions;
            comparison.ProjectBaseline = new ComparisonByRefEntity
            {
                Project = projectName,
                Experiment = baseline.Name,
                Set = baseline.BaselineSet ?? baseline.LastSet,
                Results = baseline.AggregateSetByRef(baseline.BaselineSet ?? baseline.LastSet, baselineFiltered),
            };
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the experiment info
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        var experimentFiltered = experiment.Filter(includeTags, excludeTags);
        experiment.MetricDefinitions = comparison.MetricDefinitions;

        // get the experiment baseline
        if (string.Equals(experiment.Baseline, ":project", StringComparison.OrdinalIgnoreCase))
        {
            comparison.ExperimentBaseline = comparison.ProjectBaseline;
        }
        else
        {
            comparison.ExperimentBaseline = new ComparisonByRefEntity
            {
                Project = projectName,
                Experiment = experiment.Name,
                Set = experiment.BaselineSet ?? experiment.FirstSet,
                Results = experiment.AggregateSetByRef(experiment.BaselineSet ?? experiment.FirstSet, experimentFiltered),
            };
        }

        // get the set experiment
        comparison.ExperimentSet = new ComparisonByRefEntity
        {
            Project = projectName,
            Experiment = experiment.Name,
            Set = setName,
            Results = experiment.AggregateSetByRef(setName, experimentFiltered),
        };

        // run policies
        // if (comparison.ChosenResultsForChosenExperiment is not null
        //     && comparison.BaselineResultsForChosenExperiment is not null)
        // {
        //     var policy = new PercentImprovement();
        //     foreach (var (key, result) in comparison.ChosenResultsForChosenExperiment)
        //     {
        //         if (comparison.BaselineResultsForChosenExperiment.TryGetValue(key, out var baseline))
        //         {
        //             policy.Evaluate(result, baseline, comparison.MetricDefinitions);
        //         }
        //     }
        //     this.logger.LogWarning("policy passed? {0}, {1}, {2}", policy.IsPassed, policy.NumResultsThatPassed, policy.NumResultsThatFailed);
        //     this.logger.LogWarning(policy.Requirement);
        //     this.logger.LogWarning(policy.Actual);
        // }

        return Ok(comparison);
    }

    [HttpGet("{experimentName}/sets/{setName}")]
    public async Task<ActionResult<Comparison>> GetNamedSet(
        [FromServices] IConfig config,
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromRoute] string setName,
        CancellationToken cancellationToken,
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        // init
        var metricDefinitions = (await storageService.GetMetricsAsync(projectName, cancellationToken))
            .ToDictionary(x => x.Name);

        // get the experiment and filter the results
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        var experimentFiltered = experiment.Filter(includeTags, excludeTags);
        experiment.MetricDefinitions = metricDefinitions;

        // get the results
        var results = experiment.AggregateSetByEachResult(setName, experimentFiltered)
            ?? Enumerable.Empty<Result>();

        // add the support docs
        if (!string.IsNullOrEmpty(config.PATH_TEMPLATE))
        {
            foreach (var result in results)
            {
                if (!string.IsNullOrEmpty(result.InferenceUri)) result.InferenceUri = string.Format(config.PATH_TEMPLATE, result.InferenceUri);
                if (!string.IsNullOrEmpty(result.EvaluationUri)) result.EvaluationUri = string.Format(config.PATH_TEMPLATE, result.EvaluationUri);
            }
        }

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
