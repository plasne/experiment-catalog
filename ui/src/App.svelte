<script lang="ts">
  import ExperimentsList from "./lib/ExperimentsList.svelte";
  import ExperimentPage from "./lib/ExperimentPage.svelte";
  import SetPage from "./lib/SetPage.svelte";
  import ProjectsList from "./lib/ProjectsList.svelte";

  let project: Project;
  let experiment: Experiment;
  let setName: string;

  const selectProject = (event: CustomEvent<Project>) => {
    project = event.detail;
  };

  const unselectProject = () => {
    project = undefined;
  };

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
  {#if project && experiment && setName}
    <SetPage on:unselectSet={unselectSet} {project} {experiment} {setName} />
  {:else if project && experiment}
    <ExperimentPage
      on:unselectExperiment={unselectExperiment}
      on:selectSet={selectSet}
      {project}
      {experiment}
    />
  {:else if project}
    <ExperimentsList
      on:select={selectExperiment}
      on:unselectProject={unselectProject}
      {project}
    />
  {:else}
    <ProjectsList on:select={selectProject} />
  {/if}
</main>
