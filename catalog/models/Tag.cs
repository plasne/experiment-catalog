using System.Collections.Generic;
using Newtonsoft.Json;

namespace Catalog;

public class Tag
{
    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }

    [JsonProperty("refs", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Refs { get; set; } = null;
}