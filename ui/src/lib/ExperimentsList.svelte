<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ExperimentCard from "./ExperimentCard.svelte";

  export let projectName: string;
  let experiments: Experiment[] = [];
  let state: "loading" | "loaded" | "error" = "loading";

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  const dispatch = createEventDispatcher();

  const fetchExperiments = async () => {
    try {
      state = "loading";
      const response = await fetch(
        `${prefix}/api/projects/${projectName}/experiments`
      );
      experiments = await response.json();
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  $: fetchExperiments();

  const select = (event: CustomEvent<Experiment>) => {
    dispatch("select", event.detail);
  };
</script>

<h1>Experiments in {projectName}</h1>

{#if state === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/src/assets/spinner.gif" />
  </div>
{:else if state === "error"}
  <div>Error loading experiments.</div>
{:else}
  <div class="flex-container">
    {#each experiments as experiment (experiment.name)}
      <ExperimentCard on:select={select} {experiment} />
    {/each}
  </div>
{/if}

<style>
  .flex-container {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-around;
  }
</style>
