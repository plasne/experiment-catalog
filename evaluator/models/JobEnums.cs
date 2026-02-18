using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Evaluator;

[JsonConverter(typeof(StringEnumConverter))]
public enum JobStage
{
    [EnumMember(Value = "inference")]
    Inference,

    [EnumMember(Value = "evaluation")]
    Evaluation,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum JobOutcome
{
    [EnumMember(Value = "success")]
    Success,

    [EnumMember(Value = "failed")]
    Failed,
}
