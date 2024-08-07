<script lang="ts">
  import ComparisonTable from "./ComparisonTable.svelte";
  import { createEventDispatcher } from "svelte";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;

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
</script>

<button class="link" on:click={unselectExperiment}>back</button>
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
    on:drilldown={selectSet}
    on:changeSetList={changeSetList}
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
