using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Catalog;

public class Experiment
{
    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }

    [JsonProperty("hypothesis", Required = Required.Always)]
    public required string Hypothesis { get; set; }

    [JsonProperty("results", NullValueHandling = NullValueHandling.Ignore)]
    public List<Result>? Results { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public List<Annotation>? Annotations { get; set; }

    [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    private static Result Aggregate(IEnumerable<Result> from, bool includeAnnotationsWithRef)
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

        result.Metrics = metrics.ToDictionary(x => x.Key, x =>
        {
            return new Metric
            {
                Count = x.Value.Count,
                Value = x.Value.Average(y => y.Value),
                StdDev = x.Value.StdDev(y => y.Value)
            };
        });

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
        var result = Aggregate(filtered, false);
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
            var result = Aggregate(group, true);
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

    public List<Result> AggregateLastSets(int count)
    {
        var results = new List<Result>();
        if (this.Results is null) return results;

        var queue = new Queue<Result>(this.Results.AsEnumerable().Reverse());
        var sets = new HashSet<string>();
        while (results.Count < count && queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next is null) break;
            if (!string.IsNullOrEmpty(next.Set) && sets.Contains(next.Set)) continue;

            var result = this.AggregateSet(next.Set);
            if (result is not null) results.Add(result);
            sets.Add(next.Set!);
        }

        results.Reverse();
        return results;
    }

    public Result? AggregateBaselineSet()
    {
        return this.AggregateSet(this.Results?.LastOrDefault(x => x.IsBaseline)?.Set);
    }

    public Dictionary<string, Result>? AggregateBaselineSetByRef()
    {
        return this.AggregateSetByRef(this.Results?.LastOrDefault(x => x.IsBaseline)?.Set);
    }

    public List<Result> GetAllResultsOfSet(string name)
    {
        return this.Results?.Where(x => x.Set == name).ToList() ?? [];
    }
}