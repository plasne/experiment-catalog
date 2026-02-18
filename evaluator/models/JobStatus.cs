using System.Collections.Generic;
using Newtonsoft.Json;

namespace Evaluator;

public class JobStageStatus
{
    [JsonProperty("stage")]
    public JobStage Stage { get; set; }

    [JsonProperty("succeeded")]
    public int Succeeded { get; set; }

    [JsonProperty("failed")]
    public int Failed { get; set; }
}

public class JobStatus
{
    [JsonProperty("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonProperty("total_items")]
    public int TotalItems { get; set; }

    [JsonProperty("stages")]
    public List<JobStageStatus> Stages { get; set; } = [];
}
