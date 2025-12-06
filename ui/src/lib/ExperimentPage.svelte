<script lang="ts">
  import ComparisonTable from "./ComparisonTable.svelte";
  import { createEventDispatcher } from "svelte";
  import type { ViewConfig } from "./Tools";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;
  export let config: ViewConfig = {};

  const dispatch = createEventDispatcher();

  // Local state initialized from config
  let checked: string = config.checked_metrics ?? "";
  let tags: string = config.tags ?? "";

  const emitConfigChange = () => {
    const newConfig: ViewConfig = { ...config };
    if (checked) {
      newConfig.checked_metrics = checked;
    } else {
      delete newConfig.checked_metrics;
    }
    if (tags) {
      newConfig.tags = tags;
    } else {
      delete newConfig.tags;
    }
    dispatch("changeConfig", newConfig);
  };

  const unselectExperiment = () => {
    dispatch("unselectExperiment");
  };

  const selectSet = (event: CustomEvent<string>) => {
    dispatch("selectSet", event.detail);
  };

  const changeSetList = (event: CustomEvent<string>) => {
    dispatch("changeSetList", event.detail);
  };

  const changeChecked = (event: CustomEvent<string>) => {
    checked = event.detail;
    emitConfigChange();
  };

  const changeTags = (event: CustomEvent<string>) => {
    tags = event.detail;
    emitConfigChange();
  };

  const useTheProjectBaseline = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/sets/:project/baseline`,
      {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
        },
      }
    );
    if (response.ok) {
      comparisonTable.reload();
      confirmUseTheProjectBaseline = false;
    }
  };

  const setAsProjectBaseline = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/baseline`,
      {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
        },
      }
    );
    if (response.ok) {
      comparisonTable.reload();
      confirmSetAsProjectBaseline = false;
    }
  };

  const computePValues = async () => {
    const response = await fetch(`${prefix}/api/analysis/p-values`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        project: project.name,
        experiment: experiment.name,
      }),
    });
    if (response.ok) {
      alert("Refresh in a few minutes to see the p-values.");
      confirmComputePValues = false;
    }
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let confirmUseTheProjectBaseline = false;
  let confirmSetAsProjectBaseline = false;
  let confirmComputePValues = false;
  let comparisonTable: ComparisonTable;
  let pvalueOpen = false;
</script>

<button class="link" on:click={unselectExperiment}>back</button>
<h1>PROJECT: {project.name}</h1>
<h2>EXPERIMENT: {experiment.name}</h2>
<div>
  <span>
    <label style="display:inline-flex; align-items:center; gap:0.5rem;">
      <input
        type="checkbox"
        bind:checked={confirmUseTheProjectBaseline}
        aria-label="Confirm set as project baseline"
      />
      <button
        class="link"
        on:click={useTheProjectBaseline}
        disabled={!confirmUseTheProjectBaseline}
      >
        use the project baseline
      </button>
    </label>
  </span>
</div>
<div>
  <span>
    <label style="display:inline-flex; align-items:center; gap:0.5rem;">
      <input
        type="checkbox"
        bind:checked={confirmSetAsProjectBaseline}
        aria-label="Confirm set as project baseline"
      />
      <button
        class="link"
        on:click={setAsProjectBaseline}
        disabled={!confirmSetAsProjectBaseline}
      >
        set this experiment as the project baseline
      </button>
    </label>
  </span>
</div>
<div>
  <span>
    <label style="display:inline-flex; align-items:center; gap:0.5rem;">
      <input
        type="checkbox"
        bind:checked={confirmComputePValues}
        aria-label="Confirm compute p-values"
      />
      <button
        class="link"
        on:click={computePValues}
        disabled={!confirmComputePValues}
      >
        compute p-values for this experiment
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
<div>
  <span class="label">Legend:</span>
  <span
    >[value] ([standard-deviation]) [change-vs-experiment-baseline]
    x[number-of-values] p=[p-value]</span
  >
</div>
<div class="pvalue-row">
  <button class="label pvalue-label" on:click={() => (pvalueOpen = !pvalueOpen)}
    >P-value:</button
  >
  <span
    >A measure of statistical significance - low values (&lt; 0.05) suggest the
    observed difference is unlikely due to chance alone.</span
  >
</div>
{#if pvalueOpen}
  <div class="pvalue-details">
    <p>
      The code calculates p-values using a paired permutation test with
      sign-flipping. For each metric, it first collects paired observations
      (values that exist in both baseline and experiment for the same
      reference), then computes the paired differences (experiment - baseline).
      The observed mean difference is calculated from these pairs. To generate
      the null distribution, the test randomly flips the sign of each paired
      difference (simulating the null hypothesis that there's no systematic
      difference between conditions) across many permutations (configured via
      CALC_PVALUES_USING_X_SAMPLES). The two-tailed p-value is then calculated
      as the proportion of permuted mean differences that are as extreme or more
      extreme than the observed mean difference, using the formula (extremeCount
      + 1) / (numSamples + 1) to ensure the p-value is never exactly zero.
    </p>
    <p style="margin-top: 0.5rem;">
      <strong>What it means:</strong> A low p-value (typically &lt; 0.05) suggests
      the observed difference between the experiment and baseline is unlikely to
      have occurred by chance alone, indicating statistical significance. A high
      p-value suggests the difference could reasonably be due to random variation,
      meaning there's no strong evidence of a real effect.
    </p>
  </div>
{/if}
{#if experiment.annotations}
  {#each experiment.annotations as annotation}
    <div>
      <span class="label">Annotation:</span>
      <span>{annotation.text}</span>
    </div>
  {/each}
{/if}

<div class="table">
  <ComparisonTable
    {project}
    {experiment}
    {setList}
    {checked}
    initialTags={tags}
    bind:this={comparisonTable}
    on:drilldown={selectSet}
    on:changeSetList={changeSetList}
    on:changeChecked={changeChecked}
    on:changeTags={changeTags}
  />
</div>

<style>
  .label {
    text-align: right;
    font-weight: bold;
    width: 100px;
    display: inline-block;
  }

  .pvalue-label {
    cursor: pointer;
    text-decoration: underline;
    background: none;
    border: none;
    padding: 0;
    font: inherit;
    color: inherit;
  }

  .pvalue-details {
    margin-left: 1rem;
    line-height: 1.4;
  }

  .table {
    margin-top: 2rem;
  }
</style>
