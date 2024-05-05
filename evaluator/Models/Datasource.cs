using System.Text.Json.Serialization;

public class Datasource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}