public class Result
{
    public string? Description { get; set; }
    public string? Set { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public Dictionary<string, Metric>? Metrics { get; set; }
    public bool IsBaseline { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
}