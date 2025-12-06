// ViewConfig holds display/filter state that can be encoded in the URL
export interface ViewConfig {
    checked_metrics?: string;   // metric highlighting (comma-separated)
    metrics?: string[];         // which metrics to display
    tags?: string;              // tag filter querystring
}

export function encodeConfig(config: ViewConfig): string | null {
    // Remove empty/undefined values before encoding
    const cleanConfig: ViewConfig = {};
    if (config.checked_metrics) cleanConfig.checked_metrics = config.checked_metrics;
    if (config.metrics?.length) cleanConfig.metrics = config.metrics;
    if (config.tags) cleanConfig.tags = config.tags;

    // Return null if config is empty
    if (Object.keys(cleanConfig).length === 0) return null;

    const json = JSON.stringify(cleanConfig);
    return btoa(json);
}

export function decodeConfig(encoded: string): ViewConfig {
    if (!encoded) return {};
    try {
        const json = atob(encoded);
        return JSON.parse(json) as ViewConfig;
    } catch {
        return {};
    }
}

export function updateURL(project: string = null, experiment: string = null, page: string = null, config: ViewConfig = null) {
    let url = `${window.location.pathname}`;
    var parts: string[] = [];
    if (project) parts.push(`project=${project}`);
    if (experiment) parts.push(`experiment=${experiment}`);
    if (page) parts.push(`page=${page}`);
    const encodedConfig = config ? encodeConfig(config) : null;
    if (encodedConfig) parts.push(`config=${encodedConfig}`);
    if (parts.length > 0) url += `?${parts.join("&")}`;
    window.history.pushState(null, '', url);
}

export async function loadExperiment(projectName: string, experimentName: string) {
    let prefix =
        window.location.hostname === "localhost" ? "http://localhost:6010" : "";
    const response = await fetch(
        `${prefix}/api/projects/${projectName}/experiments/${experimentName}`
    );
    var experiment = await response.json();
    return experiment
}

export function sortMetrics(
    metric_definitions: Record<string, MetricDefinition>,
    a: string,
    b: string,
) {
    // get metric definitions
    const defA = metric_definitions?.[a];
    const defB = metric_definitions?.[b];

    // if a metric definition is missing, push it to the end
    const orderA = defA?.order ?? Number.MAX_SAFE_INTEGER;
    const orderB = defB?.order ?? Number.MAX_SAFE_INTEGER;
    if (orderA !== orderB) return orderA - orderB;

    // fall back to case-insensitive alphabetical order
    return a.localeCompare(b, undefined, {
        sensitivity: "base",
        numeric: true,
    });
}