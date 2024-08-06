using System.Collections.Generic;
using Newtonsoft.Json;

namespace Catalog;

public class Comparison
{
    [JsonProperty("baseline_result_for_project", NullValueHandling = NullValueHandling.Ignore)]
    public Result? BaselineResultForBaselineExperiment { get; set; }

    [JsonProperty("baseline_result_for_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public Result? BaselineResultForChosenExperiment { get; set; }

    [JsonProperty("sets_for_experiment", NullValueHandling = NullValueHandling.Ignore)]
    public IList<Result>? SetsForChosenExperiment { get; set; }
}