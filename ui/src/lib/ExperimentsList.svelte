<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ExperimentCard from "./ExperimentCard.svelte";

  export let projectName: string;
  let experiments: Experiment[] = [];

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  const dispatch = createEventDispatcher();

  const fetchExperiments = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${projectName}/experiments`
    );
    experiments = await response.json();
  };

  $: fetchExperiments();

  const select = (event: CustomEvent<Experiment>) => {
    dispatch("select", event.detail);
  };
</script>

<h1>Experiments in {projectName}</h1>
<div class="flex-container">
  {#each experiments as experiment (experiment.name)}
    <ExperimentCard on:select={select} {experiment} />
  {/each}
</div>

<style>
  .flex-container {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-around;
  }
</style>
