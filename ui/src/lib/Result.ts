interface Result {
    ref?: string;
    set?: string;
    result_uri?: string;
    desc?: string;
    metrics?: Record<string, Metric>;
    annotations?: Annotation[];
    is_baseline: boolean;
    created: Date;
}