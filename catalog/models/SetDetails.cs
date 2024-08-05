using System.Collections.Generic;
using Newtonsoft.Json;

namespace Catalog;

public class SetDetails
{
    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }

    [JsonProperty("annotations", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<Annotation>? Annotations { get; set; }
}