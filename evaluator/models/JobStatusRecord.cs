using System;
using Newtonsoft.Json;

namespace Evaluator;

public class JobStatusRecord
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("stage")]
    public JobStage Stage { get; set; }

    [JsonProperty("status")]
    public JobOutcome Status { get; set; }

    [JsonProperty("err", NullValueHandling = NullValueHandling.Ignore)]
    public string? Error { get; set; }

    [JsonProperty("ts")]
    public DateTimeOffset Timestamp { get; set; }
}
