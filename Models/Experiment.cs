public class Experiment
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Hypothesis { get; set; }
    public IEnumerable<Result>? Results { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    private Result? GetRelatedSet(Result? found)
    {
        if (found is null) return null;
        if (found.Set is null) return found;

        var result = new Result
        {
            Set = found.Set
        };

        var metrics = new Dictionary<string, List<Metric>>();
        foreach (var r in this.Results!.Where(x => x.Set == found.Set))
        {
            if (result.Description is null && r.Description is not null) result.Description = r.Description;
            if (r.Metrics is null) continue;
            if (r.Metrics is not null)
            {
                foreach (var (key, metric) in r.Metrics)
                {
                    if (!metrics.ContainsKey(key)) metrics[key] = new List<Metric>();
                    metrics[key].Add(metric);
                }
            }
        }

        result.Metrics = metrics.ToDictionary(x => x.Key, x =>
        {
            return new Metric
            {
                Value = x.Value.Average(y => y.Value),
                StdDev = x.Value.StdDev(y => y.Value)
            };
        });

        return result;
    }

    public Result? GetFirstSet()
    {
        var first = this.Results?.FirstOrDefault();
        return GetRelatedSet(first);
    }

    public Result? GetLastSet()
    {
        var last = this.Results?.LastOrDefault();
        return GetRelatedSet(last);
    }

    public Result? GetBaselineSet()
    {
        var baseline = this.Results?.FirstOrDefault(x => x.IsBaseline);
        return GetRelatedSet(baseline);
    }
}