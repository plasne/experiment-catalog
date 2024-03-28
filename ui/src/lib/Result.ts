interface Result {
    description?: string;
    set?: string;
    metadata?: Record<string, string>;
    metrics?: Record<string, Metric>;
    isBaseline: boolean;
    created: Date;
}