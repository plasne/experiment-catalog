using Newtonsoft.Json;

public class PipelineRequest
{
    [JsonProperty("id", Required = Required.Always)]
    public required string Id { get; set; }

    [JsonProperty("ground_truth_uri", Required = Required.Always)]
    public required string GroundTruthUri { get; set; }

    [JsonProperty("project", Required = Required.Always)]
    public required string Project { get; set; }

    [JsonProperty("experiment", Required = Required.Always)]
    public required string Experiment { get; set; }

    [JsonProperty("ref", Required = Required.Always)]
    public required string Ref { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    public required string Set { get; set; }

    [JsonProperty("is_baseline", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsBaseline { get; set; }
}