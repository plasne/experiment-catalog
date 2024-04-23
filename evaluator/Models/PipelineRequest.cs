using System.Text.Json.Serialization;

public class PipelineRequest
{
    [JsonPropertyName("ground_truth_uri")]
    public string? GroundTruthUri { get; set; }

    [JsonPropertyName("inference_uri")]
    public string? InferenceUri { get; set; }

    [JsonPropertyName("evaluation_uri")]
    public string? EvaluationUri { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("experiment")]
    public string? Experiment { get; set; }

    [JsonPropertyName("ref")]
    public string? Ref { get; set; }

    [JsonPropertyName("set")]
    public string? Set { get; set; }

    [JsonPropertyName("is_baseline")]
    public bool IsBaseline { get; set; }
}