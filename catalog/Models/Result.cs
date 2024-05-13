using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class Result
{
    [JsonProperty("ref", Required = Required.Always)]
    public string? Ref { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    public string? Set { get; set; }

    [JsonProperty("inference_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? InferenceUri { get; set; }

    [JsonProperty("evaluation_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? EvaluationUri { get; set; }

    [JsonProperty("metrics", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Metric>? Metrics { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<Annotation>? Annotations { get; set; }

    [JsonProperty("is_baseline", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsBaseline { get; set; }

    [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime Created { get; set; } = DateTime.UtcNow;
}