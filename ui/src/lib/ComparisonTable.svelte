<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";

  export let projectName: string;
  export let experiment: Experiment;

  const dispatch = createEventDispatcher();

  const selectSet = (event: CustomEvent<string>) => {
    dispatch("selectSet", event.detail);
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let comparison: Comparison;
  let metrics: string[] = [];

  const fetchComparison = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/compare?count=3`
    );
    comparison = await response.json();
    const allKeys = [
      ...(comparison.last_result_for_baseline_experiment
        ? Object.keys(comparison.last_result_for_baseline_experiment.metrics)
        : []),
      ...(comparison.baseline_result_for_chosen_experiment
        ? Object.keys(comparison.baseline_result_for_chosen_experiment.metrics)
        : []),
      ...(comparison.last_results_for_chosen_experiment
        ? comparison.last_results_for_chosen_experiment.flatMap((experiment) =>
            Object.keys(experiment.metrics)
          )
        : []),
    ];
    metrics = [...new Set(allKeys)];
  };

  $: fetchComparison();
</script>

{#if comparison}
  <table>
    <thead>
      <tr>
        <th></th>
        <th>
          <ComparisonTableHeader
            title="Project Baseline"
            result={comparison.last_result_for_baseline_experiment}
            on:selectSet={selectSet}
          />
        </th>
        <th>
          <ComparisonTableHeader
            title="Experiment Baseline"
            result={comparison.baseline_result_for_chosen_experiment}
            on:selectSet={selectSet}
          />
        </th>
        {#each comparison.last_results_for_chosen_experiment as result}
          <th>
            <ComparisonTableHeader title="" {result} on:selectSet={selectSet} />
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
              result={comparison.last_result_for_baseline_experiment}
              baseline={comparison.baseline_result_for_chosen_experiment}
              {metric}
            /></td
          >
          <td
            ><ComparisonTableMetric
              result={comparison.baseline_result_for_chosen_experiment}
              {metric}
            /></td
          >
          {#each comparison?.last_results_for_chosen_experiment as result}
            <td
              ><ComparisonTableMetric
                {result}
                baseline={comparison.baseline_result_for_chosen_experiment}
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
</style>
