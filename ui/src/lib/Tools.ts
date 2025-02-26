// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

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