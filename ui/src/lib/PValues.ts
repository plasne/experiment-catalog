interface PValues {
    project: string,
    experiment: string,
    set?: string,
    result?: Result,
    p_values?: Record<string, Metric>,
}
