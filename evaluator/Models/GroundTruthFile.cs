using System.Text.Json.Serialization;

public class GroundTruthFile
{
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }
}