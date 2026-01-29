using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Catalog;

public enum MeaningfulTagsComparisonMode
{
    Baseline,
    Zero,
    Average
}

public class MeaningfulTagsRequest
{
    [JsonProperty("project", Required = Required.Always)]
    [Required, ValidName]
    public required string Project { get; set; }

    [JsonProperty("experiment", Required = Required.Always)]
    [Required, ValidName]
    public required string Experiment { get; set; }

    [JsonProperty("set", Required = Required.Always)]
    [Required, ValidName]
    public required string Set { get; set; }

    [JsonProperty("metric", Required = Required.Always)]
    [Required, ValidName]
    public required string Metric { get; set; }

    [JsonProperty("exclude_tags")]
    [ValidNames]
    public IEnumerable<string>? ExcludeTags { get; set; } = null;

    [JsonProperty("compare_to")]
    public MeaningfulTagsComparisonMode CompareTo { get; set; } = MeaningfulTagsComparisonMode.Baseline;
}