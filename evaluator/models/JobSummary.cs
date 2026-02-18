using System;
using Newtonsoft.Json;

namespace Evaluator;

public class JobSummary
{
    [JsonProperty("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonProperty("project")]
    public string Project { get; set; } = string.Empty;

    [JsonProperty("experiment")]
    public string Experiment { get; set; } = string.Empty;

    [JsonProperty("set")]
    public string Set { get; set; } = string.Empty;

    [JsonProperty("total_items")]
    public int TotalItems { get; set; }

    [JsonProperty("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonProperty("completed_at", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonProperty("inference_succeeded", NullValueHandling = NullValueHandling.Ignore)]
    public int? InferenceSucceeded { get; set; }

    [JsonProperty("inference_failed", NullValueHandling = NullValueHandling.Ignore)]
    public int? InferenceFailed { get; set; }

    [JsonProperty("evaluation_succeeded", NullValueHandling = NullValueHandling.Ignore)]
    public int? EvaluationSucceeded { get; set; }

    [JsonProperty("evaluation_failed", NullValueHandling = NullValueHandling.Ignore)]
    public int? EvaluationFailed { get; set; }
}
