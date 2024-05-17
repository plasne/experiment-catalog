using Newtonsoft.Json;

namespace Evaluator;

public class GroundTruthFile
{
    [JsonProperty("ref", Required = Required.Always)]
    public required string Ref { get; set; }
}