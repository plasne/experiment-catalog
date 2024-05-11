using Newtonsoft.Json;

public class PipelineRequest
{
    [JsonProperty("ground_truth_uri", Required = Required.Always)]
    public required string GroundTruthUri { get; set; }

    [JsonProperty("inference_uri", Required = Required.Always)]
    public required string InferenceUri { get; set; }

    [JsonProperty("evaluation_uri", Required = Required.Always)]
    public required string EvaluationUri { get; set; }

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