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
}
