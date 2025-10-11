interface Comparison {
    metric_definitions: Record<string, MetricDefinition>;
    baseline_result_for_project: Result;
    baseline_result_for_experiment: Result;
    sets_for_experiment: Result[];
}