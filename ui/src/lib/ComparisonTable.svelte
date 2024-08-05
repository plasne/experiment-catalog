<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import TagsFilter from "./TagsFilter.svelte";

  export let project: Project;
  export let experiment: Experiment;

  let state: "loading" | "loaded" | "error" = "loading";
  let compareCount = "3";
  let sets = [];

  const dispatch = createEventDispatcher();

  const selectSet = (event: CustomEvent<string>) => {
    dispatch("selectSet", event.detail);
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let comparison: Comparison;
  let metrics: string[] = [];
  let tagFilters: string;

  const updateSetsPerCompareCount = () => {
    const count = parseInt(compareCount, 10);
    while (sets.length < count) {
      sets.push("*");
    }
    while (sets.length > count) {
      sets.pop();
    }
  };

  const fetchComparison = async () => {
    try {
      state = "loading";
      updateSetsPerCompareCount();
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/compare?sets=${sets.join(",")}&${tagFilters ?? ""}`,
      );
      comparison = await response.json();
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
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  const changeSet = async () => {
    const selects = document.querySelectorAll("button[data-set-options]");
    sets = Array.from(selects).map(
      (select) => (select as HTMLButtonElement).innerText,
    );
  };

  $: fetchComparison(), tagFilters, sets;
</script>

{#if comparison}
  <div class="selection">
    <TagsFilter {project} bind:querystring={tagFilters} />
    <span>last:</span>
    <select bind:value={compareCount} on:change={fetchComparison}>
      <option value="1">1</option>
      <option value="2">2</option>
      <option value="3">3</option>
      <option value="4">4</option>
      <option value="5">5</option>
      <option value="10">10</option>
      <option value="20">20</option>
      <option value="30">30</option>
      <option value="100">100</option>
    </select>
    <span>of {comparison.set_details.length} experiments</span>
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
        {#each comparison.sets_for_experiment as result}
          <th>
            <ComparisonTableHeader
              title=""
              {result}
              details={comparison.set_details}
              on:selectSet={selectSet}
              on:changeSet={changeSet}
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
          {#each comparison?.sets_for_experiment as result}
            <td
              ><ComparisonTableMetric
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

  select {
    text-align: center;
    font-size: 1rem;
  }
</style>
