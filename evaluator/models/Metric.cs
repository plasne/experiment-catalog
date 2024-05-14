using Newtonsoft.Json;

namespace Evaluator;

public class Metric
{
    [JsonProperty("value", Required = Required.Always)]
    public required decimal Value { get; set; }
}