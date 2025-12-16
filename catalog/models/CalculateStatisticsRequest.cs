using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Catalog;

public class CalculateStatisticsRequest
{
    [JsonProperty("project", Required = Required.Always)]
    public required string Project { get; set; }

    [JsonProperty("experiment", Required = Required.Always)]
    public required string Experiment { get; set; }
}