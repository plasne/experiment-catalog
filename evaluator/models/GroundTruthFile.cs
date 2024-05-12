using Newtonsoft.Json;

public class GroundTruthFile
{
    [JsonProperty("ref", Required = Required.Always)]
    public required string Ref { get; set; }
}