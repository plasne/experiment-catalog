public class Result
{
    public string? Ref { get; set; }
    public string? Set { get; set; }
    public string? ResultUri { get; set; }
    public string? Desc { get; set; }
    public Dictionary<string, Metric>? Metrics { get; set; }
    public bool IsBaseline { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
}