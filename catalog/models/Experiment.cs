using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Catalog;

public class Experiment()
{
    public static readonly string[] namesIndicatingClassification = ["accuracy", "precision", "recall"];

    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }

    [JsonProperty("hypothesis", Required = Required.Always)]
    public required string Hypothesis { get; set; }

    [JsonProperty("results", NullValueHandling = NullValueHandling.Ignore)]
    public List<Result>? Results { get; set; }

    [JsonProperty("baseline", NullValueHandling = NullValueHandling.Ignore)]
    public string? Baseline { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public List<Annotation>? Annotations { get; set; }

    [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Dictionary<string, MetricDefinition>? MetricDefinitions { get; set; }

    private bool TryReduceAsCost(string key, AggregateFunctions func, IList<string> tags, List<Metric> metrics, out Metric metric)
    {
        metric = new Metric();

        if (func == AggregateFunctions.Cost ||
            (
                func == AggregateFunctions.Default &&
                key.Contains("cost", StringComparison.InvariantCultureIgnoreCase)
            )
        )
        {
            metric.Count = metrics.Count;
            metric.Value = metrics.Sum(x => x.Value);
            if (!tags.Contains("cost")) tags.Add("cost");
            metric.Tags = tags;
            return true;
        }

        return false;
    }

    private bool TryReduceAsCount(string key, AggregateFunctions func, IList<string> tags, List<Metric> metrics, out Metric metric)
    {
        metric = new Metric();

        if (func == AggregateFunctions.Cost ||
            (
                func == AggregateFunctions.Default &&
                key.Contains("count", StringComparison.InvariantCultureIgnoreCase)
            )
        )
        {
            metric.Count = metrics.Count;
            metric.Value = metrics.Sum(x => x.Value);
            if (!tags.Contains("count")) tags.Add("count");
            metric.Tags = tags;
            return true;
        }

        return false;
    }

    private bool TryReduceAsAccuracy(string key, AggregateFunctions func, IList<string> tags, List<Metric> metrics, out Metric metric)
    {
        metric = new Metric();

        if (func == AggregateFunctions.Accuracy ||
            (
                func == AggregateFunctions.Default &&
                key.Contains("accuracy", StringComparison.InvariantCultureIgnoreCase) &&
                metrics.Exists(x => x.Classification is not null)
            )
        )
        {
            var t = metrics.Count(x => x.Classification is not null && x.Classification.StartsWith('t'));
            var a = metrics.Count(x => x.Classification is not null);
            metric.Count = metrics.Count;
            metric.Value = t.DivBy(a);
            metric.Normalized = metric.Value;
            if (!tags.Contains("accuracy")) tags.Add("accuracy");
            metric.Tags = tags;
            return true;
        }

        return false;
    }

    private bool TryReduceAsPrecision(string key, AggregateFunctions func, IList<string> tags, List<Metric> metrics, out Metric metric)
    {
        metric = new Metric();

        if (func == AggregateFunctions.Precision ||
            (
                func == AggregateFunctions.Default &&
                key.Contains("precision", StringComparison.InvariantCultureIgnoreCase) &&
                metrics.Exists(x => x.Classification is not null)
            )
        )
        {
            var tp = metrics.Count(x => x.Classification is not null && x.Classification == "t+");
            var p = metrics.Count(x => x.Classification is not null && x.Classification.EndsWith('+'));
            metric.Count = metrics.Count;
            metric.Value = tp.DivBy(p);
            metric.Normalized = metric.Value;
            if (!tags.Contains("precision")) tags.Add("precision");
            metric.Tags = tags;
            return true;
        }

        return false;
    }

    private bool TryReduceAsRecall(string key, AggregateFunctions func, IList<string> tags, List<Metric> metrics, out Metric metric)
    {
        metric = new Metric();

        if (func == AggregateFunctions.Recall ||
            (
                func == AggregateFunctions.Default &&
                key.Contains("recall", StringComparison.InvariantCultureIgnoreCase) &&
                metrics.Exists(x => x.Classification is not null)
            )
        )
        {
            var tp = metrics.Count(x => x.Classification is not null && x.Classification == "t+");
            var fn = metrics.Count(x => x.Classification is not null && x.Classification == "f-");
            metric.Count = metrics.Count;
            metric.Value = tp.DivBy(tp + fn);
            metric.Normalized = metric.Value;
            if (!tags.Contains("recall")) tags.Add("recall");
            metric.Tags = tags;
            return true;
        }

        return false;
    }

    private Metric ReduceAsAverage(string key, IList<string> tags, List<Metric> metrics)
    {
        var average = metrics.Average(x => x.Value);
        decimal? normalized = (this.MetricDefinitions is not null &&
            this.MetricDefinitions.TryGetValue(key, out var definition) &&
            definition.TryNormalize(average, out var x))
            ? x : null;

        if (!tags.Contains("average")) tags.Add("average");

        return new Metric
        {
            Count = metrics.Count,
            Value = average,
            Normalized = normalized,
            StdDev = metrics.StdDev(x => x.Value),
            Tags = tags,
        };
    }

    private Metric Reduce(string key, List<Metric> metrics)
    {
        Metric metric;

        AggregateFunctions func = AggregateFunctions.Default;
        IList<string>? ntags = null;
        if (this.MetricDefinitions is not null && this.MetricDefinitions.TryGetValue(key, out var definition))
        {
            func = definition.AggregateFunction;
            ntags = definition.Tags;
        }
        IList<string> tags = ntags ?? [];

        if (TryReduceAsCost(key, func, tags, metrics, out metric)) return metric;
        if (TryReduceAsCount(key, func, tags, metrics, out metric)) return metric;
        if (TryReduceAsAccuracy(key, func, tags, metrics, out metric)) return metric;
        if (TryReduceAsPrecision(key, func, tags, metrics, out metric)) return metric;
        if (TryReduceAsRecall(key, func, tags, metrics, out metric)) return metric;
        return ReduceAsAverage(key, tags, metrics);
    }

    private Result Aggregate(IEnumerable<Result> from, bool includeAnnotationsWithRef)
    {
        var result = new Result();
        var annotations = new List<Annotation>();

        var metrics = new Dictionary<string, List<Metric>>();
        foreach (var r in from)
        {
            if (r.Annotations is not null
                && (includeAnnotationsWithRef || string.IsNullOrEmpty(r.Ref)))
            {
                annotations.AddRange(r.Annotations);
            }
            if (r.Metrics is null) continue;
            foreach (var (key, metric) in r.Metrics)
            {
                if (!metrics.ContainsKey(key)) metrics[key] = [];
                metrics[key].Add(metric);
            }
        }

        result.Metrics = metrics.ToDictionary(x => x.Key, x => this.Reduce(x.Key, x.Value));

        if (annotations.Count > 0)
        {
            result.Annotations = annotations;
        }
        return result;
    }

    public Result? AggregateSet(string? set)
    {
        if (string.IsNullOrEmpty(set)) return null;
        if (this.Results is null) return null;
        var filtered = this.Results.Where(x => x.Set == set);
        var result = this.Aggregate(filtered, false);
        result.Set = set;
        return result;
    }

    public Dictionary<string, Result>? AggregateSetByRef(string? set)
    {
        if (string.IsNullOrEmpty(set)) return null;
        if (this.Results is null) return null;
        var results = new Dictionary<string, Result>();

        var filtered = this.Results.Where(x => x.Set == set && !string.IsNullOrEmpty(x.Ref));
        foreach (var group in filtered.GroupBy(x => x.Ref))
        {
            var result = this.Aggregate(group, true);
            result.Ref = group.Key;
            result.Set = set;
            results.Add(group.Key!, result);
        }

        return results;
    }

    public Result? AggregateFirstSet()
    {
        return this.AggregateSet(this.Results?.FirstOrDefault()?.Set);
    }

    public Dictionary<string, Result>? AggregateFirstSetByRef()
    {
        return this.AggregateSetByRef(this.Results?.FirstOrDefault()?.Set);
    }

    public Result? AggregateLastSet()
    {
        return this.AggregateSet(this.Results?.LastOrDefault()?.Set);
    }

    public Dictionary<string, Result>? AggregateLastSetByRef()
    {
        return this.AggregateSetByRef(this.Results?.LastOrDefault()?.Set);
    }

    public List<Result> AggregateAllSets()
    {
        var results = new List<Result>();
        if (this.Results is null) return results;

        foreach (var set in this.Sets)
        {
            var result = this.AggregateSet(set);
            if (result is not null) results.Add(result);
        }

        return results;
    }

    public Result? AggregateBaselineSet()
    {
        return this.AggregateSet(this.Baseline);
    }

    public Dictionary<string, Result>? AggregateBaselineSetByRef()
    {
        return this.AggregateSetByRef(this.Baseline);
    }

    public List<Result> GetAllResultsOfSet(string name)
    {
        return this.Results?.Where(x => x.Set == name).ToList() ?? [];
    }

# pragma warning disable S3776 // Cognitive Complexity of this method is not too high
    public void Filter(IEnumerable<Tag>? includeTags, IEnumerable<Tag>? excludeTags)
    {
        var hasIncludeTags = includeTags is not null && includeTags.Any();
        var hasExcludeTags = excludeTags is not null && excludeTags.Any();
        if (!hasIncludeTags && !hasExcludeTags) return;
        this.Results = this.Results?
            .Where(x =>
            {
                var hasAnnotations = x.Annotations is not null && x.Annotations.Count > 0;
                var hasMetrics = x.Metrics is not null && x.Metrics.Count > 0;
                if (hasAnnotations && !hasMetrics) return true;
                if (x.Ref is null) return false;
                if (hasExcludeTags)
                {
                    foreach (var tag in excludeTags!)
                    {
                        if (tag.Refs is not null && tag.Refs.Contains(x.Ref)) return false;
                    }
                }
                if (hasIncludeTags)
                {
                    foreach (var tag in includeTags!)
                    {
                        if (tag.Refs is not null && tag.Refs.Contains(x.Ref)) return true;
                    }
                }
                if (hasIncludeTags) return false;
                if (hasExcludeTags) return true;
                return true;
            }).ToList();
    }
# pragma warning restore S3776

    public IList<string> Sets
    {
        get => this.Results?
            .Select(x => x.Set)
            .Distinct()
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList() ?? [];
    }
}