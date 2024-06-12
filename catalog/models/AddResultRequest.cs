using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Catalog;

public class AddResultRequest
{
    private static readonly string[] classifications = ["t+", "t-", "f+", "f-"];

    [JsonProperty("ref", Required = Required.Always)]
    public required string Ref { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    public required string Set { get; set; }

    [JsonProperty("inference_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? InferenceUri { get; set; }

    [JsonProperty("evaluation_uri", NullValueHandling = NullValueHandling.Ignore)]
    public string? EvaluationUri { get; set; }

    [JsonProperty("metrics", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, object>? Metrics { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public List<Annotation>? Annotations { get; set; }

    [JsonProperty("is_baseline", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsBaseline { get; set; }

    public Dictionary<string, Metric>? ToMetrics()
    {
        if (this.Metrics is null) return null;
        var metrics = new Dictionary<string, Metric>();
        foreach (var metric in this.Metrics)
        {
            var str = metric.Value.ToString();
            if (decimal.TryParse(str, out var dec))
            {
                metrics[metric.Key] = new Metric { Value = dec };
            }
            else if (classifications.Contains(str)
                && Array.Exists(Experiment.namesIndicatingClassification, x => metric.Key.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                metrics[metric.Key] = new Metric { Classification = str };
            }
            else
            {
                throw new HttpException(400, "Invalid metric value.");
            }
        }
        return metrics;
    }
}