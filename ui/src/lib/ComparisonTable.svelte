<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import TagsFilter from "./TagsFilter.svelte";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;

  let state: "loading" | "loaded" | "error" = "loading";
  let compareCount = 3;
  let controls = [];
  let selected: Result[];

  const dispatch = createEventDispatcher();

  const drilldown = (event: CustomEvent<string>) => {
    dispatch("drilldown", event.detail);
  };

  const updateSetList = () => {
    if (!selected) return;
    setList = selected.map((result) => result?.set).join(",");
    console.info("setList", setList);
    dispatch("changeSetList", setList);
  };

  const applySetList = () => {
    selected = [];
    if (setList) {
      console.info("no set", setList);
      var setListSplit = setList.split(",");
      for (var i = 0; i < compareCount; i++) {
        const result =
          i < setListSplit.length
            ? comparison.sets_for_experiment.find(
                (result) => result.set === setListSplit[i],
              )
            : null;
        selected[i] = result;
      }
    } else {
      console.info("use last");
      selected = comparison.sets_for_experiment.slice(-compareCount);
    }
    console.info("from apply");
    updateSetList();
  };

  const select = (event: CustomEvent<{ index: number; result: Result }>) => {
    selected[event.detail.index] = event.detail.result;
    console.info("from select");
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
        `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/compare?${tagFilters ?? ""}`,
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
              Object.keys(experiment.metrics),
            )
          : []),
      ];
      metrics = [...new Set(allKeys)];

      // apply the set list
      applySetList();

      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  $: fetchComparison(), tagFilters;
</script>

{#if comparison}
  <div class="selection">
    <TagsFilter {project} bind:querystring={tagFilters} />
    <span>last:</span>
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
    <span>of {comparison.sets_for_experiment.length} experiments</span>
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
        <tr>
          <td class="label">{metric}</td>
          <td
            ><ComparisonTableMetric
              result={comparison.baseline_result_for_project}
              baseline={comparison.baseline_result_for_experiment}
              {metric}
            /></td
          >
          <td
            ><ComparisonTableMetric
              result={comparison.baseline_result_for_experiment}
              {metric}
            /></td
          >
          {#each selected as result, index}
            <td
              ><ComparisonTableMetric
                bind:this={controls[index]}
                {result}
                baseline={comparison.baseline_result_for_experiment}
                {metric}
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

  .selection {
    width: 80rem;
    text-align: right;
  }
</style>
