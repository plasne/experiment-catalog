using Newtonsoft.Json;

public class Metric
{
    [JsonProperty("count")]
    public int Count { get; set; } = 1;

    [JsonProperty("value", Required = Required.Always)]
    public required decimal Value { get; set; }

    [JsonProperty("std_dev")]
    public decimal StdDev { get; set; }
}