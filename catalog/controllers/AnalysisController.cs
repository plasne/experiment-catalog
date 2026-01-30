using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.AspNetCore.Mvc;
using NetBricks;

namespace Catalog;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    [HttpPost("statistics")]
    public IActionResult CalculateStatistics(
        [FromServices] CalculateStatisticsService calculateStatisticsService,
        [FromBody] CalculateStatisticsRequest request)
    {
        calculateStatisticsService.Enqueue(request);
        return StatusCode(201);
    }

    [HttpPost("meaningful-tags")]
    public async Task<IActionResult> MeaningfulTags(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromBody] MeaningfulTagsRequest request,
        CancellationToken cancellationToken)
    {
        var diffs = new List<TagDiff>();
        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);

        // Load experiment with only the specific set we need
        var experiment = await storageService.GetExperimentWithSetsAsync(
            request.Project,
            request.Experiment,
            [request.Set],
            cancellationToken);

        // Load baseline with only the baseline set if needed
        var baseline = request.CompareTo == MeaningfulTagsComparisonMode.Baseline
            ? await storageService.GetProjectBaselineWithBaselineSetAsync(request.Project, cancellationToken)
            : null;

        var listOfTags = await storageService.ListTagsAsync(request.Project, cancellationToken);
        var includeTags = await storageService.GetTagsAsync(request.Project, listOfTags, cancellationToken);
        var excludeTags = request.ExcludeTags is not null
            ? await storageService.GetTagsAsync(request.Project, request.ExcludeTags, cancellationToken)
            : null;

        var compareToDefault = 0.0M;
        if (request.CompareTo == MeaningfulTagsComparisonMode.Average)
        {
            var results = experiment.Filter(null, excludeTags);
            var experimentResult = experiment.AggregateSet(request.Set, results);
            Metric? experimentMetric = null;
            experimentResult?.Metrics?.TryGetValue(request.Metric, out experimentMetric);
            compareToDefault = experimentMetric?.Value ?? 0.0M;
        }

        foreach (var tag in includeTags)
        {
            var experimentResults = experiment.Filter([tag], excludeTags);
            var experimentResult = experiment.AggregateSet(request.Set, experimentResults);
            Metric? experimentTagMetric = null;
            experimentResult?.Metrics?.TryGetValue(request.Metric, out experimentTagMetric);

            decimal? compareTo = compareToDefault;
            if (baseline is not null)
            {
                var baselineResults = baseline.Filter([tag], excludeTags);
                var baselineResult = baseline.AggregateSet(baseline.BaselineSet ?? baseline.LastSet, baselineResults);
                Metric? baselineTagMetric = null;
                baselineResult?.Metrics?.TryGetValue(request.Metric, out baselineTagMetric);
                compareTo = baselineTagMetric?.Value;
            }

            if (experimentTagMetric?.Value is not null && compareTo is not null)
            {
                var diff = (decimal)(experimentTagMetric.Value - compareTo);
                diffs.Add(new TagDiff
                {
                    Tag = tag.Name,
                    Diff = diff,
                    Impact = diff * (experimentTagMetric.Count ?? 0),
                    Count = experimentTagMetric.Count,
                });
            }
        }

        return Ok(new MeaningfulTagsResponse { Tags = diffs.OrderBy(x => x.Impact) });
    }
}
