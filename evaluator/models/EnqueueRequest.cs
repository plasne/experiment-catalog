using System.Collections.Generic;
using Newtonsoft.Json;

public class EnqueueRequest
{
    [JsonProperty("project", Required = Required.Always)]
    public required string Project { get; set; }

    [JsonProperty("experiment", Required = Required.Always)]
    public required string Experiment { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    public required string Set { get; set; }

    [JsonProperty("is_baseline", Required = Required.Always)]
    public bool IsBaseline { get; set; }

    [JsonProperty("containers", Required = Required.Always)]
    public required List<string> Containers { get; set; }

    [JsonProperty("queue", Required = Required.Always)]
    public required string Queue { get; set; }

    [JsonProperty("iterations")]
    public int Iterations { get; set; } = 1;
}