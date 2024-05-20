using System.Diagnostics;

namespace Evaluator;

public static class DiagnosticService
{
    public const string SourceName = "evaluator";
    public static readonly ActivitySource Source = new(SourceName);
}
