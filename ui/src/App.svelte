<script lang="ts">
  import ExperimentsList from "./lib/ExperimentsList.svelte";
  import ExperimentPage from "./lib/ExperimentPage.svelte";

  let projectName = "project-01";
  let experiment: Experiment;

  const selectExperiment = (event: CustomEvent<Experiment>) => {
    experiment = event.detail;
  };

  const unselectExperiment = () => {
    experiment = undefined;
  };
</script>

<main>
  {#if experiment}
    <ExperimentPage
      on:unselect={unselectExperiment}
      {projectName}
      {experiment}
    />
  {:else}
    <ExperimentsList on:select={selectExperiment} {projectName} />
  {/if}
</main>
