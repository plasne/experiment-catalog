using System.Text.Json.Serialization;

public class EnqueueResponse
{
    [JsonPropertyName("successful")]
    public List<string>? Successful { get; set; }

    [JsonPropertyName("failed")]
    public List<string>? Failed { get; set; }
}