using Newtonsoft.Json;

public class Project
{
    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }
}