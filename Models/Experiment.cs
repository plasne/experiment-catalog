using Microsoft.Net.Http.Headers;

public class Experiment
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Hypothesis { get; set; }
    public string? WorkItemUri { get; set; }
    public IEnumerable<Result>? Results { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    private Result Aggregate(IEnumerable<Result> from)
    {
        var result = new Result();

        var metrics = new Dictionary<string, List<Metric>>();
        foreach (var r in from)
        {
            if (r.Metrics is null) continue;
            if (r.Metrics is not null)
            {
                foreach (var (key, metric) in r.Metrics)
                {
                    if (!metrics.ContainsKey(key)) metrics[key] = [];
                    metrics[key].Add(metric);
                }
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

        return result;
    }

    public Result? AggregateFirstSet()
    {
        if (this.Results is null) return null;
        var first = this.Results.First();
        var filtered = this.Results.Where(x => x.Set == first.Set);
        var result = this.Aggregate(filtered);
        result.Set = first.Set;
        return result;
    }

    public Result? AggregateLastSet()
    {
        if (this.Results is null) return null;
        var first = this.Results.Last();
        var filtered = this.Results.Where(x => x.Set == first.Set);
        var result = this.Aggregate(filtered);
        result.Set = first.Set;
        return result;
    }

    public List<Result> AggregateLastSets(int count)
    {
        var results = new List<Result>();
        if (this.Results is null) return results;

        var queue = new Queue<Result>(this.Results.Reverse());
        var sets = new HashSet<string>();
        while (results.Count < count && queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next is null) break;
            if (!string.IsNullOrEmpty(next.Set) && sets.Contains(next.Set)) continue;

            var filtered = this.Results.Where(x => x.Set == next.Set);
            var rs = this.Aggregate(filtered);
            rs.Set = next.Set;
            results.Add(rs);
            sets.Add(next.Set!);
        }

        results.Reverse();
        return results;
    }

    public Result? AggregateBaselineSet()
    {
        if (this.Results is null) return null;
        var baseline = this.Results.LastOrDefault(x => x.IsBaseline);
        if (baseline is null) return null;

        var filtered = this.Results.Where(x => x.Set == baseline.Set);
        var result = this.Aggregate(filtered);
        result.Set = baseline.Set;
        return result;
    }

    public List<Result> GetAllResultsOfSet(string name)
    {
        return this.Results?.Where(x => x.Set == name).ToList() ?? [];
    }
}