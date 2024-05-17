using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Evaluator;

public static class DiagnosticService
{
    const string SourceName = "evaluator";
    public static readonly ActivitySource Source = new(SourceName);
    public static readonly ActivitySource DoNotLogSource = new("do-not-log");
    static readonly Meter Metrics = new(SourceName);
    static readonly Histogram<int> TimeToFirstResponse = Metrics.CreateHistogram<int>("time_to_first_response", "ms", "Time to first response");
    static readonly Histogram<int> TimeToLastResponse = Metrics.CreateHistogram<int>("time_to_last_response", "ms", "Time to last response");

    public static void RecordTimeToFirstResponse(int time)
    {
        TimeToFirstResponse.Record(time);
    }

    public static void RecordTimeToLastResponse(int time)
    {
        TimeToLastResponse.Record(time);
    }
}
