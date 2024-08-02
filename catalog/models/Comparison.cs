using System.Collections.Generic;
using Newtonsoft.Json;

namespace Catalog;

public class Comparison
{
    [JsonProperty("total_experiment_count")]
    public int TotalExperimentCount { get; set; }

    [JsonProperty("last_result_for_baseline_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Result? LastResultForBaselineExperiment { get; set; }

    [JsonProperty("baseline_result_for_chosen_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Result? BaselineResultForChosenExperiment { get; set; }

    [JsonProperty("last_results_for_chosen_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public List<Result>? LastResultsForChosenExperiment { get; set; }
}