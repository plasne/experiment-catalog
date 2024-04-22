<script lang="ts">
  import ComparisonTable from "./ComparisonTable.svelte";
  import { createEventDispatcher } from "svelte";

  export let projectName: string;
  export let experiment: Experiment;

  const dispatch = createEventDispatcher();

  const unselectExperiment = () => {
    dispatch("unselectExperiment");
  };

  const selectSet = (event: CustomEvent<string>) => {
    dispatch("selectSet", event.detail);
  };
</script>

<button class="link-button" on:click={unselectExperiment}>back</button>
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

<div class="table">
  <ComparisonTable {projectName} {experiment} on:selectSet={selectSet} />
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
</style>
