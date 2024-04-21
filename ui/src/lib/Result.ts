interface Result {
    ref?: string;
    set?: string;
    result_uri?: string;
    desc?: string;
    metrics?: Record<string, Metric>;
    is_baseline: boolean;
    created: Date;
}