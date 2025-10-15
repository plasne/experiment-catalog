<script lang="ts">
  import ComparisonTable from "./ComparisonTable.svelte";
  import { createEventDispatcher } from "svelte";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;
  export let checked: string;

  const dispatch = createEventDispatcher();

  const unselectExperiment = () => {
    dispatch("unselectExperiment");
  };

  const selectSet = (event: CustomEvent<string>) => {
    dispatch("selectSet", event.detail);
  };

  const changeSetList = (event: CustomEvent<string>) => {
    dispatch("changeSetList", event.detail);
  };

  const changeChecked = (event: CustomEvent<Set<string>>) => {
    dispatch("changeChecked", event.detail);
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

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let confirmUseTheProjectBaseline = false;
  let confirmSetAsProjectBaseline = false;
  let comparisonTable: ComparisonTable;
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
    x[number-of-values]</span
  >
</div>
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
    bind:this={comparisonTable}
    on:drilldown={selectSet}
    on:changeSetList={changeSetList}
    on:changeChecked={changeChecked}
  />
</div>

<style>
  .label {
    text-align: right;
    font-weight: bold;
    width: 100px;
    display: inline-block;
    margin-right: 0.2rem;
  }

  .table {
    margin-top: 2rem;
  }
</style>
