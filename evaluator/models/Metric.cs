using Newtonsoft.Json;

public class Metric
{
    [JsonProperty("value", Required = Required.Always)]
    public required decimal Value { get; set; }
}