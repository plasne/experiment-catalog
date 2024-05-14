using System.Collections.Generic;
using Newtonsoft.Json;

namespace Catalog;

public class AddResultRequest
{
    [JsonProperty("ref", Required = Required.Always)]
    public required string Ref { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    public required string Set { get; set; }

    [JsonProperty("inference_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? InferenceUri { get; set; }

    [JsonProperty("evaluation_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? EvaluationUri { get; set; }

    [JsonProperty("metrics", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, decimal>? Metrics { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public List<Annotation>? Annotations { get; set; }

    [JsonProperty("is_baseline", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsBaseline { get; set; }
}