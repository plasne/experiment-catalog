using System.Text.Json.Serialization;

public class EnqueueRequest
{
    [JsonPropertyName("set")]
    public string? Set { get; set; }

    [JsonPropertyName("is_baseline")]
    public bool IsBaseline { get; set; }
}