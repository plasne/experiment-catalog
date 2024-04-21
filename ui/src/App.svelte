<script lang="ts">
  import ExperimentsList from "./lib/ExperimentsList.svelte";
  import ExperimentPage from "./lib/ExperimentPage.svelte";
  import SetPage from "./lib/SetPage.svelte";

  let projectName = "project-01";
  let experiment: Experiment;
  let setName: string;

  const selectExperiment = (event: CustomEvent<Experiment>) => {
    experiment = event.detail;
  };

  const unselectExperiment = () => {
    experiment = undefined;
  };

  const selectSet = (event: CustomEvent<string>) => {
    setName = event.detail;
  };

  const unselectSet = () => {
    setName = undefined;
  };
</script>

<main>
  {#if experiment && setName}
    <SetPage
      on:unselectSet={unselectSet}
      {projectName}
      {experiment}
      {setName}
    />
  {:else if experiment}
    <ExperimentPage
      on:unselectExperiment={unselectExperiment}
      on:selectSet={selectSet}
      {projectName}
      {experiment}
    />
  {:else}
    <ExperimentsList on:select={selectExperiment} {projectName} />
  {/if}
</main>
