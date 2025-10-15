<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import TagsFilter from "./TagsFilter.svelte";
  import { sortMetrics } from "./Tools";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;
  export let checked: string;

  let state: "loading" | "loaded" | "error" = "loading";
  let compareCount = 3;
  let controls = [];
  let selected: Result[];
  let metricsHighlighted: Set<string>;

  const dispatch = createEventDispatcher();

  const drilldown = (event: CustomEvent<string>) => {
    dispatch("drilldown", event.detail);
  };

  const updateSetList = () => {
    if (!selected) return;
    setList = selected.map((result) => result?.set).join(",");
    dispatch("changeSetList", setList);
  };

  const toggleRowCheck = (metric: string) => {
    if (!metricsHighlighted) {
      metricsHighlighted = new Set<string>([metric]);
    } else if (metricsHighlighted.has(metric)) {
      metricsHighlighted.delete(metric);
      metricsHighlighted = metricsHighlighted; // trigger reactivity
    } else {
      metricsHighlighted.add(metric);
      metricsHighlighted = metricsHighlighted; // trigger reactivity
    }
    checked = Array.from(metricsHighlighted).join(",");
    dispatch("changeChecked", checked);
  };

  const applySetList = () => {
    selected = [];
    if (setList) {
      var setListSplit = setList.split(",");
      while (
        setListSplit.length > 0 &&
        setListSplit[setListSplit.length - 1].trim() === ""
      ) {
        setListSplit.pop();
      }
      for (var i = 0; i < Math.max(compareCount, setListSplit.length); i++) {
        const result =
          i < setListSplit.length
            ? comparison.sets_for_experiment.find(
                (result) => result.set === setListSplit[i]
              )
            : null;
        selected[i] = result;
      }
    } else {
      selected = comparison.sets_for_experiment.slice(-compareCount);
    }
    updateSetList();
  };

  const select = (event: CustomEvent<{ index: number; result: Result }>) => {
    selected[event.detail.index] = event.detail.result;
    updateSetList();
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let comparison: Comparison;
  let metrics: string[] = [];
  let tagFilters: string;

  const fetchComparison = async () => {
    try {
      state = "loading";

      // fetch comparison
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/compare?${tagFilters ?? ""}`
      );
      comparison = await response.json();

      // get a list of metrics
      const allKeys = [
        ...(comparison.baseline_result_for_project
          ? Object.keys(comparison.baseline_result_for_project.metrics)
          : []),
        ...(comparison.baseline_result_for_experiment
          ? Object.keys(comparison.baseline_result_for_experiment.metrics)
          : []),
        ...(comparison.sets_for_experiment
          ? comparison.sets_for_experiment.flatMap((experiment) =>
              Object.keys(experiment.metrics)
            )
          : []),
      ];
      metrics = [...new Set(allKeys)].sort((a, b) =>
        sortMetrics(comparison.metric_definitions, a, b)
      );

      // apply the set list
      applySetList();

      // apply the checked metrics
      if (checked) {
        metricsHighlighted = new Set(checked.split(","));
      }

      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  export function reload() {
    fetchComparison();
  }

  $: fetchComparison(), tagFilters;
</script>

{#if comparison}
  <div class="selection">
    <TagsFilter {project} bind:querystring={tagFilters} />
    <span>show at least</span>
    <select bind:value={compareCount} on:change={applySetList}>
      <option value={1}>1</option>
      <option value={2}>2</option>
      <option value={3}>3</option>
      <option value={4}>4</option>
      <option value={5}>5</option>
      <option value={6}>6</option>
      <option value={7}>7</option>
      <option value={8}>8</option>
      <option value={9}>9</option>
      <option value={10}>10</option>
      <option value={20}>20</option>
      <option value={30}>30</option>
      <option value={100}>100</option>
    </select>
    <span>of {comparison.sets_for_experiment.length} permutations</span>
  </div>
{/if}

{#if state === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
  </div>
{:else if state === "error"}
  <div>Error loading comparison.</div>
{:else if comparison}
  <table>
    <thead>
      <tr>
        <th class="checkbox-column"></th>
        <th></th>
        <th>
          <ComparisonTableHeader
            title="Project Baseline"
            result={comparison.baseline_result_for_project}
            clickable={false}
          />
        </th>
        <th>
          <ComparisonTableHeader
            title="Experiment Baseline"
            result={comparison.baseline_result_for_experiment}
            clickable={false}
          />
        </th>
        {#each selected as result, index}
          <th>
            <ComparisonTableHeader
              {index}
              title=""
              {result}
              results={comparison.sets_for_experiment}
              on:drilldown={drilldown}
              on:select={select}
            />
          </th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each metrics as metric}
        <tr class:highlighted={metricsHighlighted?.has(metric)}>
          <td class="checkbox-column">
            <input
              type="checkbox"
              checked={metricsHighlighted?.has(metric)}
              on:change={() => toggleRowCheck(metric)}
            />
          </td>
          <td class="label">{metric}</td>
          <td
            ><ComparisonTableMetric
              result={comparison.baseline_result_for_project}
              baseline={comparison.baseline_result_for_experiment}
              {metric}
              definition={comparison.metric_definitions[metric]}
            /></td
          >
          <td
            ><ComparisonTableMetric
              result={comparison.baseline_result_for_experiment}
              {metric}
              definition={comparison.metric_definitions[metric]}
            /></td
          >
          {#each selected as result, index}
            <td
              ><ComparisonTableMetric
                bind:this={controls[index]}
                {result}
                baseline={comparison.baseline_result_for_experiment}
                {metric}
                definition={comparison.metric_definitions[metric]}
              /></td
            >
          {/each}
        </tr>
      {/each}
    </tbody>
  </table>
{/if}

<style>
  table {
    width: 100%;
    border-collapse: collapse;
  }

  th {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
    vertical-align: bottom;
    border-bottom: 1px solid #ddd;
  }

  td {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
  }

  td.label {
    text-align: left;
    font-weight: bold;
  }

  .checkbox-column {
    text-align: center;
  }

  .highlighted {
    background-color: #333333;
  }

  input[type="checkbox"] {
    cursor: pointer;
  }

  .selection {
    width: 80rem;
    text-align: right;
  }
</style>
