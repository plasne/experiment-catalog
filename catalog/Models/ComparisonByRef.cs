public class ComparisonByRef
{
    public Dictionary<string, Result>? LastResultsForBaselineExperiment { get; set; }
    public Dictionary<string, Result>? BaselineResultsForChosenExperiment { get; set; }
    public Dictionary<string, Result>? ChosenResultsForChosenExperiment { get; set; }
}