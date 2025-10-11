export function updateURL(project: string = null, experiment: string = null, page: string = null) {
    let url = `${window.location.pathname}`;
    var parts: string[] = [];
    if (project) parts.push(`project=${project}`);
    if (experiment) parts.push(`experiment=${experiment}`);
    if (page) parts.push(`page=${page}`);
    if (parts.length > 0) url += '?' + parts.join('&');
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