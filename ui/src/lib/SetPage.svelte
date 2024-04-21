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
  let comparison: Comparison;
  let refs: string[] = [];
  let metrics: string[] = [];

  const fetchSet = async () => {
    // get the results and comparison
    let compareUrl = `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/compare?count=0`;
    let setUrl = `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/sets/${setName}`;
    const [setResponse, compareResponse] = await Promise.all([
      fetch(setUrl),
      fetch(compareUrl),
    ]);
    comparison = await compareResponse.json();
    results = await setResponse.json();

    // get a list of all refs (we only care about those in the results)
    const allRefs = [...results.map((result) => result.ref)];
    refs = [...new Set(allRefs)];

    // get a list of all metrics
    const allKeys = [
      ...(comparison.last_result_for_baseline_experiment
        ? Object.keys(comparison.last_result_for_baseline_experiment.metrics)
        : []),
      ...(comparison.baseline_result_for_chosen_experiment
        ? Object.keys(comparison.baseline_result_for_chosen_experiment.metrics)
        : []),
      ...results.flatMap((result) => Object.keys(result.metrics)),
    ];
    metrics = [...new Set(allKeys)];
  };

  $: fetchSet();
</script>

<button class="link-button" on:click={unselectSet}>back</button>
<h1>PROJECT: {projectName}</h1>
<h2>EXPERIMENT: {experiment.name}</h2>
<div>
  <span class="label">Description:</span>
  <span>{experiment.description}</span>
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
<h3>SET: {setName}</h3>

{#if comparison}
  <table>
    <thead>
      <tr>
        <th>Source</th>
        <th>Reference</th>
        {#each metrics as metric}
          <th>{metric}</th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#if results}
        {#each refs as ref}
          <tr class="experiment-baseline">
            <td
              >Experiment Baseline / {comparison
                .last_result_for_baseline_experiment.set}</td
            >
            <td class="label">{ref}</td>
            {#each metrics as metric}
              <td>
                <ComparisonTableMetric
                  result={comparison.last_result_for_baseline_experiment}
                  {metric}
                  baseline={comparison.baseline_result_for_chosen_experiment}
                ></ComparisonTableMetric>
              </td>
            {/each}
          </tr>
          <tr class="project-baseline">
            <td
              >Project Baseline / {comparison
                .baseline_result_for_chosen_experiment.set}</td
            >
            <td class="label">{ref}</td>
            {#each metrics as metric}
              <td>
                <ComparisonTableMetric
                  result={comparison.baseline_result_for_chosen_experiment}
                  {metric}
                ></ComparisonTableMetric>
              </td>
            {/each}
          </tr>
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
                    baseline={comparison.baseline_result_for_chosen_experiment}
                    showStdDev={false}
                    showCount={false}
                  ></ComparisonTableMetric>
                </td>
              {/each}
            </tr>
          {/each}
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
    background-color: inherit;
  }

  tr.project-baseline {
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
