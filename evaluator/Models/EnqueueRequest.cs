using System.Text.Json.Serialization;

public class EnqueueRequest
{
    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("experiment")]
    public string? Experiment { get; set; }

    [JsonPropertyName("set")]
    public string? Set { get; set; }

    [JsonPropertyName("is_baseline")]
    public bool IsBaseline { get; set; }

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; } = 1;
}