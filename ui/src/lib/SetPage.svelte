<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";

  export let projectName: string;
  export let experiment: Experiment;
  export let setName: string;

  const dispatch = createEventDispatcher();

  const unselectSet = () => {
    dispatch("unselectSet");
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let results: Result[];
  let showResults = false;
  let comparison: ComparisonByRef;
  let refs: string[] = [];
  let metrics: string[] = [];

  const fetchComparison = async () => {
    // get the comparison
    let url = `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/sets/${setName}/compare-by-ref`;
    var response = await fetch(url);
    comparison = await response.json();

    // get a list of all refs in the chosen results
    refs = Object.keys(comparison.chosen_results_for_chosen_experiment);

    // get a list of all metrics
    const allMetrics = [
      ...(comparison.last_results_for_baseline_experiment
        ? Object.values(
            comparison.last_results_for_baseline_experiment
          ).flatMap((result) => Object.keys(result.metrics))
        : []),
      ...(comparison.baseline_results_for_chosen_experiment
        ? Object.values(
            comparison.baseline_results_for_chosen_experiment
          ).flatMap((result) => Object.keys(result.metrics))
        : []),
      ...(comparison.chosen_results_for_chosen_experiment
        ? Object.values(
            comparison.chosen_results_for_chosen_experiment
          ).flatMap((result) => Object.keys(result.metrics))
        : []),
    ];
    metrics = [...new Set(allMetrics)];
  };

  const fetchDetails = async () => {
    showResults = !showResults;
    if (!results) {
      let url = `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/sets/${setName}`;
      const response = await fetch(url);
      results = await response.json();
    }
  };

  $: fetchComparison();
</script>

<button class="link-button" on:click={unselectSet}>back</button>
<h1>PROJECT: {projectName}</h1>
<h2>EXPERIMENT: {experiment.name}</h2>
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
  <button class="link-button" on:click={fetchDetails}>(toggle details)</button>
</h3>

{#if comparison}
  <table>
    <thead>
      <tr>
        <th>Source</th>
        <th>Ref</th>
        {#each metrics as metric}
          <th>{metric}</th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#if comparison}
        {#each refs as ref}
          <tr class="experiment-baseline">
            <td
              >Experiment Baseline / {comparison
                .last_results_for_baseline_experiment[ref]?.set ?? "-"}</td
            >
            <td class="label">{ref}</td>
            {#each metrics as metric}
              <td>
                <ComparisonTableMetric
                  result={comparison.last_results_for_baseline_experiment[ref]}
                  {metric}
                  baseline={comparison.baseline_results_for_chosen_experiment[
                    ref
                  ]}
                ></ComparisonTableMetric>
              </td>
            {/each}
          </tr>
          <tr class="project-baseline">
            <td
              >Project Baseline / {comparison
                .baseline_results_for_chosen_experiment[ref]?.set ?? "-"}</td
            >
            <td class="label">{ref}</td>
            {#each metrics as metric}
              <td>
                <ComparisonTableMetric
                  result={comparison.baseline_results_for_chosen_experiment[
                    ref
                  ]}
                  {metric}
                ></ComparisonTableMetric>
              </td>
            {/each}
          </tr>
          <tr class="set-aggregate">
            <td
              >Set Aggregate / {comparison.chosen_results_for_chosen_experiment[
                ref
              ].set}</td
            >
            <td class="label">{ref}</td>
            {#each metrics as metric}
              <td>
                <ComparisonTableMetric
                  result={comparison.chosen_results_for_chosen_experiment[ref]}
                  {metric}
                  baseline={comparison.baseline_results_for_chosen_experiment[
                    ref
                  ]}
                ></ComparisonTableMetric>
              </td>
            {/each}
          </tr>
          {#if showResults && results}
            {#each results.filter((x) => x.ref === ref) as result}
              <tr>
                <td>
                  {#if result.result_uri}
                    <button
                      class="link-button"
                      on:click={() => window.open(result.result_uri, "_blank")}
                      >Set / {result.set}</button
                    >
                  {:else}
                    Set / {result.set}
                  {/if}
                </td>
                <td class="label">{result.ref}</td>
                {#each metrics as metric}
                  <td>
                    <ComparisonTableMetric
                      {result}
                      {metric}
                      baseline={comparison
                        .baseline_results_for_chosen_experiment[ref]}
                      showStdDev={false}
                      showCount={false}
                    ></ComparisonTableMetric>
                  </td>
                {/each}
              </tr>
            {/each}
          {/if}
          <tr><td>&nbsp;</td></tr>
        {/each}
      {/if}
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

  .link-button {
    background: none;
    border: none;
    cursor: pointer;
    padding: 0;
    font-size: inherit;
    font-weight: inherit;
    color: inherit;
    text-decoration: underline;
    text-decoration-color: #777;
  }

  .link-button:hover {
    text-decoration: underline;
  }

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
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
  }

  td.label {
    text-align: left;
    font-weight: bold;
  }
</style>
