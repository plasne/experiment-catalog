<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import Annotations from "./Annotations.svelte";

  export let project: Project;
  export let experiment: Experiment;
  export let setName: string;

  let state: "loading" | "loaded" | "error" = "loading";

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
    try {
      state = "loading";
      // get the comparison
      let url = `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/${setName}/compare-by-ref`;
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
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  const fetchDetails = async () => {
    try {
      state = "loading";
      showResults = !showResults;
      if (!results) {
        let url = `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/${setName}`;
        const response = await fetch(url);
        results = await response.json();
      }
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  $: fetchComparison();
</script>

<button class="link" on:click={unselectSet}>back</button>
<h1>PROJECT: {project.name}</h1>
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
  <button class="link" on:click={fetchDetails}>(toggle details)</button>
</h3>

{#if state === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
  </div>
{:else if state === "error"}
  <div>Error loading data.</div>
{:else if comparison}
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
      {#each refs as ref}
        <tr class="experiment-baseline">
          <td
            ><nobr
              >Experiment Baseline / {comparison
                .last_results_for_baseline_experiment?.[ref]?.set ?? "-"}</nobr
            ></td
          >
          <td class="label">{ref}</td>
          {#each metrics as metric}
            <td>
              <ComparisonTableMetric
                result={comparison.last_results_for_baseline_experiment?.[ref]}
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
            ><nobr
              >Project Baseline / {comparison
                .baseline_results_for_chosen_experiment[ref]?.set ?? "-"}</nobr
            ></td
          >
          <td class="label">{ref}</td>
          {#each metrics as metric}
            <td>
              <ComparisonTableMetric
                result={comparison.baseline_results_for_chosen_experiment?.[
                  ref
                ]}
                {metric}
              ></ComparisonTableMetric>
            </td>
          {/each}
        </tr>
        <tr class="set-aggregate">
          <td
            ><nobr
              >Set Aggregate / {comparison.chosen_results_for_chosen_experiment[
                ref
              ].set}</nobr
            ></td
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
        <tr>
          <td colspan={2 + metrics.length}>
            <Annotations
              result={comparison.chosen_results_for_chosen_experiment[ref]}
            />
          </td>
        </tr>
        {#if showResults && results}
          {#each results.filter((x) => x.ref === ref) as result}
            <tr>
              <td>
                {#if result.evaluation_uri}
                  <button
                    class="link"
                    on:click={() =>
                      window.open(result.evaluation_uri, "_blank")}
                    ><nobr>Set / {result.set}</nobr></button
                  >
                {:else}
                  <nobr>Set / {result.set}</nobr>
                {/if}
              </td>
              <td class="label">{result.ref}</td>
              {#each metrics as metric}
                <td>
                  <ComparisonTableMetric
                    {result}
                    {metric}
                    baseline={comparison.baseline_results_for_chosen_experiment[
                      ref
                    ]}
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
