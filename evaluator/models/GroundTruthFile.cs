using System.Text.Json.Serialization;

public class GroundTruthFile
{
    [JsonPropertyName("ref")]
    [YamlDotNet.Serialization.YamlMember(Alias = "ref")]
    public string? Ref { get; set; }
}