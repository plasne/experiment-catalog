using System;
using Iso8601DurationHelper;

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

    public static (string container, string blob) GetBlobRefForStage(this PipelineRequest req, Stages stage)
    {
        var uri = stage switch
        {
            Stages.GroundTruth => req.GroundTruthUri,
            Stages.Inference => req.InferenceUri,
            Stages.Evaluation => req.EvaluationUri,
            _ => throw new Exception($"unknown stage: {stage}"),
        };

        var parts = uri.Split("/", 2);
        if (parts.Length != 2)
        {
            throw new Exception($"expected a container and blob name separated by a /");
        }

        return (parts[0], parts[1]);
    }
}