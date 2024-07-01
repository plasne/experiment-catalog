using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Evaluator;

public class PipelineRequest
{
    [JsonProperty("run_id", Required = Required.Always)]
    public required Guid RunId { get; set; }

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

    [JsonProperty("inf_headers", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? InferenceHeaders { get; set; }

    [JsonProperty("eval_headers", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? EvaluationHeaders { get; set; }
}