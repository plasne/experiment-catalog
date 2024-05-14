using System.Collections.Generic;
using Newtonsoft.Json;

public class ComparisonByRef
{
    [JsonProperty("last_results_for_baseline_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Result>? LastResultsForBaselineExperiment { get; set; }

    [JsonProperty("baseline_results_for_chosen_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Result>? BaselineResultsForChosenExperiment { get; set; }

    [JsonProperty("chosen_results_for_chosen_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Result>? ChosenResultsForChosenExperiment { get; set; }
}