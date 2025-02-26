// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
        var comparison = new Comparison();
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        var metricDefinitions = (await storageService.GetMetricsAsync(projectName, cancellationToken))
            .ToDictionary(x => x.Name);

        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            baseline.Filter(includeTags, excludeTags);
            baseline.MetricDefinitions = metricDefinitions;
            comparison.BaselineResultForBaselineExperiment =
                baseline.AggregateBaselineSet()
                ?? baseline.AggregateLastSet();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison data
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        experiment.Filter(includeTags, excludeTags);
        experiment.MetricDefinitions = metricDefinitions;
        comparison.BaselineResultForChosenExperiment =
            string.Equals(experiment.Baseline, ":project", StringComparison.OrdinalIgnoreCase)
            ? comparison.BaselineResultForBaselineExperiment :
            experiment.AggregateBaselineSet()
            ?? experiment.AggregateFirstSet();
        comparison.SetsForChosenExperiment = experiment.AggregateAllSets();

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
        var metricDefinitions = (await storageService.GetMetricsAsync(projectName, cancellationToken))
            .ToDictionary(x => x.Name);


        // get the baseline
        try
        {
            var baseline = await storageService.GetProjectBaselineAsync(projectName, cancellationToken);
            baseline.Filter(includeTags, excludeTags);
            baseline.MetricDefinitions = metricDefinitions;
            comparison.LastResultsForBaselineExperiment =
                baseline.AggregateBaselineSetByRef()
                ?? baseline.AggregateLastSetByRef();
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Failed to get baseline experiment for project {projectName}.", projectName);
        }

        // get the comparison datas
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        experiment.Filter(includeTags, excludeTags);
        experiment.MetricDefinitions = metricDefinitions;
        comparison.BaselineResultsForChosenExperiment =
            string.Equals(experiment.Baseline, ":project", StringComparison.OrdinalIgnoreCase)
            ? comparison.LastResultsForBaselineExperiment :
            experiment.AggregateBaselineSetByRef()
            ?? experiment.AggregateFirstSetByRef();
        comparison.ChosenResultsForChosenExperiment = experiment.AggregateSetByRef(setName);

        // run policies
        if (comparison.ChosenResultsForChosenExperiment is not null
            && comparison.BaselineResultsForChosenExperiment is not null)
        {
            var policy = new PercentImprovement();
            foreach (var (key, result) in comparison.ChosenResultsForChosenExperiment)
            {
                if (comparison.BaselineResultsForChosenExperiment.TryGetValue(key, out var baseline))
                {
                    policy.Evaluate(result, baseline, metricDefinitions);
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
        [FromServices] IConfig config,
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromRoute] string experimentName,
        [FromRoute] string setName,
        CancellationToken cancellationToken,
        [FromQuery(Name = "include-tags")] string includeTagsStr = "",
        [FromQuery(Name = "exclude-tags")] string excludeTagsStr = "")
    {
        var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
        var (includeTags, excludeTags) = await LoadTags(storageService, projectName, includeTagsStr, excludeTagsStr, cancellationToken);
        experiment.Filter(includeTags, excludeTags);
        var results = experiment.GetAllResultsOfSet(setName);
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
