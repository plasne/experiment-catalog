using System.Text.Json.Serialization;

public class GroundTruthPayload
{
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }
}