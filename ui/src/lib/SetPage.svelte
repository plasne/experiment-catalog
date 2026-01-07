<script lang="ts">
  import { onMount, tick } from "svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import Annotations from "./Annotations.svelte";
  import MetricsFilter from "./MetricsFilter.svelte";
  import TagsFilter from "./TagsFilter.svelte";
  import FreeFilter from "./FreeFilter.svelte";
  import { sortMetrics, type ViewConfig } from "./Tools";

  interface Props {
    project: Project;
    experiment: Experiment;
    setName: string;
    config?: ViewConfig;
    onunselectSet?: () => void;
    onchangeConfig?: (config: ViewConfig) => void;
  }

  let {
    project,
    experiment,
    setName,
    config = {},
    onunselectSet,
    onchangeConfig,
  }: Props = $props();

  let loadingState: "loading" | "loaded" | "error" = $state("loading");

  const unselectSet = () => {
    onunselectSet?.();
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let results: Result[] = $state();
  let showResults = $state(false);
  let baselineResults: Result[] = $state();
  let showBaselineResults = $state(false);
  let comparison: ComparisonByRef = $state();
  let masterRefs: string[] = $state([]);
  let filteredRefs: string[] = $state([]);
  let metrics: string[] = $state([]);
  let selectedMetrics: string[] = $state([]);
  let metricDefinitions: MetricDefinition[] = $state([]);
  let tagFilters: string = $state("");
  let filterFunc: Function = $state();

  // Pre-computed maps for O(1) lookup instead of O(n) filter in template
  let resultsByRef: Map<string, Result[]> = $state(new Map());
  let baselineResultsByRef: Map<string, Result[]> = $state(new Map());

  // Build lookup maps when results change
  const buildResultsMap = () => {
    const map = new Map<string, Result[]>();
    if (results) {
      for (const result of results) {
        if (!map.has(result.ref)) {
          map.set(result.ref, []);
        }
        map.get(result.ref)!.push(result);
      }
    }
    resultsByRef = map;
  };

  const buildBaselineResultsMap = () => {
    const map = new Map<string, Result[]>();
    if (baselineResults) {
      for (const result of baselineResults) {
        if (!map.has(result.ref)) {
          map.set(result.ref, []);
        }
        map.get(result.ref)!.push(result);
      }
    }
    baselineResultsByRef = map;
  };

  const emitConfigChange = () => {
    const newConfig: ViewConfig = { ...config };
    if (
      selectedMetrics.length > 0 &&
      selectedMetrics.length !== metrics.length
    ) {
      newConfig.metrics = selectedMetrics;
    } else {
      delete newConfig.metrics;
    }
    if (tagFilters) {
      newConfig.tags = tagFilters;
    } else {
      delete newConfig.tags;
    }
    onchangeConfig?.(newConfig);
  };

  const fetchComparison = async () => {
    try {
      loadingState = "loading";
      // get the comparison
      let url = `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/${setName}/compare-by-ref?${tagFilters ?? ""}`;
      var response = await fetch(url);
      comparison = await response.json();

      // get a list of all refs in the chosen results
      masterRefs = Object.keys(comparison.experiment_set?.results ?? {});
      applyFilter();

      // get a list of all metrics
      const allMetrics = [
        ...(comparison.project_baseline?.results
          ? Object.values(comparison.project_baseline.results).flatMap(
              (result) => Object.keys(result.metrics)
            )
          : []),
        ...(comparison.experiment_baseline?.results
          ? Object.values(comparison.experiment_baseline.results).flatMap(
              (result) => Object.keys(result.metrics)
            )
          : []),
        ...(comparison.experiment_set?.results
          ? Object.values(comparison.experiment_set.results).flatMap((result) =>
              Object.keys(result.metrics)
            )
          : []),
      ];
      metrics = [...new Set(allMetrics)].sort((a, b) =>
        sortMetrics(comparison.metric_definitions, a, b)
      );

      // populate metric definitions for the filter
      metricDefinitions = metrics
        .map((name) => comparison.metric_definitions[name])
        .filter((def) => def !== undefined);

      // reset selectedMetrics when metric definitions change
      if (config.metrics?.length) {
        // Use metrics from config if available
        selectedMetrics = config.metrics.filter((m) => metrics.includes(m));
      } else if (metricDefinitions.length <= 10) {
        selectedMetrics = metrics;
      } else {
        selectedMetrics = [];
      }

      // Mark as initialized after first load
      await tick();
      initialized = true;

      loadingState = "loaded";
    } catch (error) {
      console.error(error);
      loadingState = "error";
    }
  };

  const fetchDetails = async () => {
    try {
      loadingState = "loading";
      showResults = !showResults;
      if (!results) {
        let url = `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/${setName}`;
        const response = await fetch(url);
        results = await response.json();
        buildResultsMap();
      }
      loadingState = "loaded";
    } catch (error) {
      console.error(error);
      loadingState = "error";
    }
  };

  const fetchBaselineDetails = async () => {
    try {
      loadingState = "loading";
      showBaselineResults = !showBaselineResults;
      if (!baselineResults && comparison.experiment_baseline?.set) {
        let url = `${prefix}/api/projects/${comparison.experiment_baseline.project}/experiments/${comparison.experiment_baseline.experiment}/sets/${comparison.experiment_baseline.set}`;
        const response = await fetch(url);
        baselineResults = await response.json();
        buildBaselineResultsMap();
      }
      loadingState = "loaded";
    } catch (error) {
      console.error(error);
      loadingState = "error";
    }
  };

  const delay = (ms: number) =>
    new Promise((resolve) => setTimeout(resolve, ms));

  const applyFilter = async () => {
    if (!filterFunc) {
      filteredRefs = [...masterRefs];
    } else {
      filteredRefs = masterRefs.filter((ref) => {
        return filterFunc(
          comparison.experiment_baseline?.results?.[ref],
          comparison.experiment_set?.results?.[ref]
        );
      });
    }
  };

  const filter = async (func: Function | undefined) => {
    loadingState = "loading";
    await delay(0);

    filterFunc = func;
    applyFilter();
    loadingState = "loaded";
  };

  const setAsExperimentBaseline = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/${setName}/baseline`,
      {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
        },
      }
    );
    if (response.ok) {
      fetchComparison();
      confirmBaseline = false;
    }
  };

  var confirmBaseline = $state(false);
  let initialized = $state(false);

  // Called when metrics filter changes
  const onMetricsChange = () => {
    if (initialized) {
      emitConfigChange();
    }
  };

  // Called when tag filters change (via apply button in TagsFilter)
  const onTagFiltersChange = (newTagFilters: string) => {
    tagFilters = newTagFilters;
    emitConfigChange();
    fetchComparison();
  };

  // Initial fetch on mount
  onMount(() => {
    tagFilters = config.tags ?? "";
    fetchComparison();
  });
</script>

<button class="link" onclick={unselectSet}>back</button>
<h1>PROJECT: {project.name}</h1>
<h2>EXPERIMENT: {experiment.name}</h2>
<div>
  <span>
    <label style="display:inline-flex; align-items:center; gap:0.5rem;">
      <input
        type="checkbox"
        bind:checked={confirmBaseline}
        aria-label="Confirm set as project baseline"
      />
      <button
        class="link"
        onclick={setAsExperimentBaseline}
        disabled={!confirmBaseline}
      >
        set this permutation as the experiment baseline
      </button>
    </label>
  </span>
</div>
<div>
  <span class="label">Hypothesis:</span>
  <span>{experiment.hypothesis}</span>
</div>
<div>
  <span class="label">Created:</span>
  <span>
    {new Intl.DateTimeFormat("en-US", {
      year: "numeric",
      month: "long",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(new Date(experiment.created))}
  </span>
</div>
<h3>
  <span>SET: {setName}</span>
  <button class="link" onclick={fetchDetails}>(toggle set iterations)</button>
  <button class="link" onclick={fetchBaselineDetails}
    >(toggle baseline iterations)</button
  >
</h3>

{#if comparison}
  <div class="selection">
    <MetricsFilter
      {metricDefinitions}
      bind:selectedMetrics
      onchange={onMetricsChange}
    />
    <br />
    <TagsFilter
      {project}
      bind:querystring={tagFilters}
      onapply={onTagFiltersChange}
    />
    <br />
    <FreeFilter
      onfilter={filter}
      {metrics}
      filteredCount={filteredRefs.length}
      totalCount={masterRefs.length}
    />
  </div>
{/if}

{#if loadingState === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
  </div>
{:else if loadingState === "error"}
  <div>Error loading data.</div>
{:else if comparison}
  <table>
    <thead>
      <tr>
        <th>Source</th>
        <th>Ref</th>
        {#each selectedMetrics as metric}
          <th>{metric}</th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each filteredRefs as ref}
        <tr class="experiment-baseline">
          <td
            ><nobr
              >Project Baseline / {comparison.project_baseline?.set ??
                "-"}</nobr
            ></td
          >
          <td class="label"><nobr>{ref}</nobr></td>
          {#each selectedMetrics as metric}
            <td>
              <ComparisonTableMetric
                result={comparison.project_baseline?.results?.[ref]}
                {metric}
                baseline={comparison.experiment_baseline?.results?.[ref]}
                definition={comparison.metric_definitions[metric]}
              ></ComparisonTableMetric>
            </td>
          {/each}
        </tr>
        <tr class="project-baseline">
          <td
            ><nobr
              >Experiment Baseline / {comparison.experiment_baseline?.set ??
                "-"}</nobr
            ></td
          >
          <td class="label"><nobr>{ref}</nobr></td>
          {#each selectedMetrics as metric}
            <td>
              <ComparisonTableMetric
                result={comparison.experiment_baseline?.results?.[ref]}
                {metric}
                definition={comparison.metric_definitions[metric]}
              ></ComparisonTableMetric>
            </td>
          {/each}
        </tr>
        {#if showBaselineResults && baselineResults}
          {#each baselineResultsByRef.get(ref) ?? [] as result}
            <tr>
              <td>
                <nobr>Baseline / {result.set}</nobr>
                {#if result.inference_uri}
                  <button
                    class="link"
                    onclick={() => window.open(result.inference_uri, "_blank")}
                    >(inf)</button
                  >
                {/if}
                {#if result.evaluation_uri}
                  <button
                    class="link"
                    onclick={() => window.open(result.evaluation_uri, "_blank")}
                    >(eval)</button
                  >
                {/if}
              </td>
              <td class="label"><nobr>{result.ref}</nobr></td>
              {#each selectedMetrics as metric}
                <td>
                  <ComparisonTableMetric
                    {result}
                    {metric}
                    baseline={comparison.experiment_baseline?.results?.[ref]}
                    showStdDev={false}
                    showCount={false}
                    definition={comparison.metric_definitions[metric]}
                  ></ComparisonTableMetric>
                </td>
              {/each}
            </tr>
          {/each}
        {/if}
        <tr class="set-aggregate">
          <td
            ><nobr
              >Set Aggregate / {comparison.experiment_set?.set ??
                "MISSING"}</nobr
            ></td
          >
          <td class="label"><nobr>{ref}</nobr></td>
          {#each selectedMetrics as metric}
            <td>
              <ComparisonTableMetric
                result={comparison.experiment_set?.results?.[ref]}
                {metric}
                baseline={comparison.experiment_baseline?.results?.[ref]}
                definition={comparison.metric_definitions[metric]}
              ></ComparisonTableMetric>
            </td>
          {/each}
        </tr>
        <tr>
          <td colspan={2 + selectedMetrics.length}>
            <Annotations entity={comparison.experiment_set?.[ref]} />
          </td>
        </tr>
        {#if showResults && results}
          {#each resultsByRef.get(ref) ?? [] as result}
            <tr>
              <td>
                <nobr>Set / {result.set}</nobr>
                {#if result.inference_uri}
                  <button
                    class="link"
                    onclick={() => window.open(result.inference_uri, "_blank")}
                    >(inf)</button
                  >
                {/if}
                {#if result.evaluation_uri}
                  <button
                    class="link"
                    onclick={() => window.open(result.evaluation_uri, "_blank")}
                    >(eval)</button
                  >
                {/if}
              </td>
              <td class="label"><nobr>{result.ref}</nobr></td>
              {#each selectedMetrics as metric}
                <td>
                  <ComparisonTableMetric
                    {result}
                    {metric}
                    baseline={comparison.experiment_baseline?.results?.[ref]}
                    showStdDev={false}
                    showCount={false}
                    definition={comparison.metric_definitions[metric]}
                  ></ComparisonTableMetric>
                </td>
              {/each}
            </tr>
          {/each}
        {/if}
        <tr><td>&nbsp;</td></tr>
      {/each}
    </tbody>
  </table>
{/if}

<style>
  .label {
    text-align: right;
    font-weight: bold;
    width: 100px;
    display: inline-block;
    margin-right: 0.2rem;
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  table thead {
    position: sticky;
    top: 0;
    background-color: #373;
    z-index: 1;
    border-bottom: 1px solid #ddd;
  }

  th {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
    vertical-align: bottom;
  }

  tr.experiment-baseline {
    background-color: #444;
  }

  tr.project-baseline {
    background-color: #454;
  }

  tr.set-aggregate {
    background-color: #444;
  }

  td {
    padding-left: 1.5em;
    padding-right: 1.5em;
    text-align: left;
  }

  td.label {
    text-align: left;
    font-weight: bold;
    padding-right: 3rem;
  }

  .selection {
    width: 100em;
    margin-bottom: 1em;
  }
</style>
