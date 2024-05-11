using System;
using Iso8601DurationHelper;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

public static class Ext
{
    public static Duration AsDuration(this string value, Func<Duration> dflt)
    {
        if (Duration.TryParse(value, out var duration))
        {
            return duration;
        }
        return dflt();
    }

    public static T Deserialize<T>(this string value, string filepath)
    {
        if (filepath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
        {
            var obj = JsonConvert.DeserializeObject<T>(value)
                ?? throw new Exception($"deserialization of '{filepath}' using JSON failed.");
            return obj;
        }
        else if (filepath.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase))
        {
            var yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var obj = yamlDeserializer.Deserialize<T>(value)
                ?? throw new Exception($"deserialization of '{filepath}' using YAML failed.");
            return obj;
        }
        else
        {
            throw new Exception($"deserialization of '{filepath}' failed because the file type was not supported.");
        }
    }
}