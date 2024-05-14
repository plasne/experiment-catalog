using Newtonsoft.Json;

namespace Catalog;

public class Metric
{
    [JsonIgnore]
    public bool IsValueOnly = false;

    [JsonProperty("count")]
    public int Count { get; set; } = 1;

    [JsonProperty("value", Required = Required.Always)]
    public required decimal Value { get; set; }

    [JsonProperty("std_dev")]
    public decimal StdDev { get; set; }

    public bool ShouldSerializeCount()
    {
        return !this.IsValueOnly;
    }

    public bool ShouldSerializeStdDev()
    {
        return !this.IsValueOnly;
    }
}