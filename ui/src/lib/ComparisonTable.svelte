<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import TagsFilter from "./TagsFilter.svelte";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;

  let state: "loading" | "loaded" | "error" = "loading";
  let controls = [];
  let selected: Result[];

  const dispatch = createEventDispatcher();

  const drilldown = (event: CustomEvent<string>) => {
    dispatch("drilldown", event.detail);
  };

  const updateSetList = () => {
    if (!selected) return;
    setList = selected.map((result) => result?.set).join(",");
    dispatch("changeSetList", setList);
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

      // determine the results that are selected
      console.info("fetch comparison");
      selected = [null, null, null, null, null];
      if (setList) {
        var setListSplit = setList.split(",");
        for (var i = 0; i < Math.max(setListSplit.length, 5); i++) {
          selected[i] = comparison.sets_for_experiment.find(
            (result) => result.set === setListSplit[i],
          );
        }
      } else {
        selected = comparison.sets_for_experiment.slice(-5);
      }
      updateSetList();

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
    <div class="selection">
      <TagsFilter {project} bind:querystring={tagFilters} />
      <span
        >{comparison.sets_for_experiment.length} runs of this experiment</span
      >
    </div>
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
