interface ComparisonByRef {
    metric_definitions: Record<string, MetricDefinition>;
    last_results_for_baseline_experiment: Record<string, Result>,
    baseline_results_for_chosen_experiment: Record<string, Result>,
    chosen_results_for_chosen_experiment: Record<string, Result>,
}