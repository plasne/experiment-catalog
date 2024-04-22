using System.Text.Json.Serialization;

public class Queue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}