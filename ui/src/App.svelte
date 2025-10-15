<script lang="ts">
  import { loadExperiment, updateURL } from "./lib/Tools";
  import ExperimentsList from "./lib/ExperimentsList.svelte";
  import ExperimentPage from "./lib/ExperimentPage.svelte";
  import SetPage from "./lib/SetPage.svelte";
  import ProjectsList from "./lib/ProjectsList.svelte";
  import { onMount } from "svelte";

  let state: "loading" | "loaded" | "error" = "loading";
  let project: Project;
  let experiment: Experiment;
  let setList: string;
  let setName: string;
  let checked: string;

  const selectProject = (event: CustomEvent<Project>) => {
    project = event.detail;
    updateURL(project.name);
  };

  const unselectProject = () => {
    project = undefined;
    setList = undefined;
    updateURL();
  };

  const selectExperiment = (event: CustomEvent<Experiment>) => {
    experiment = event.detail;
    updateURL(project.name, experiment.name);
  };

  const unselectExperiment = () => {
    experiment = undefined;
    setList = undefined;
    updateURL(project.name);
  };

  const selectSet = (event: CustomEvent<string>) => {
    setName = event.detail;
    updateURL(project.name, experiment.name, `set:${setName}`, checked);
  };

  const unselectSet = () => {
    setName = undefined;
    updateURL(project.name, experiment.name);
  };

  const changeSetList = (event: CustomEvent<string>) => {
    setList = event.detail;
    updateURL(project.name, experiment.name, `sets:${setList}`, checked);
  };

  const changeChecked = (event: CustomEvent<string>) => {
    checked = event.detail;
    updateURL(project.name, experiment.name, `sets:${setList}`, checked);
  };

  async function parseQueryString() {
    try {
      const params = new URLSearchParams(window.location.search);
      const qproject = params.get("project");
      const qexperiment = params.get("experiment");
      const qpage = params.get("page");
      const qchecked = params.get("checked");

      checked = qchecked;
      if (qproject && qexperiment && qpage && qpage.startsWith("set:")) {
        setName = qpage.slice(4);
        experiment = await loadExperiment(qproject, qexperiment);
        project = { name: qproject };
      } else if (
        qproject &&
        qexperiment &&
        qpage &&
        qpage.startsWith("sets:")
      ) {
        setList = qpage.slice(5);
        experiment = await loadExperiment(qproject, qexperiment);
        project = { name: qproject };
      } else if (qproject && qexperiment) {
        experiment = await loadExperiment(qproject, qexperiment);
        project = { name: qproject };
      } else if (qproject) {
        project = { name: qproject };
      }
    } catch {
      setName = undefined;
      experiment = undefined;
      project = undefined;
    }

    state = "loaded";
  }

  onMount(parseQueryString);
</script>

<main>
  {#if state === "loading"}
    <div>Loading...</div>
    <div>
      <img class="loading" alt="loading" src="/spinner.gif" />
    </div>
  {:else if project && experiment && setName}
    <SetPage on:unselectSet={unselectSet} {project} {experiment} {setName} />
  {:else if project && experiment}
    <ExperimentPage
      on:unselectExperiment={unselectExperiment}
      on:selectSet={selectSet}
      on:changeSetList={changeSetList}
      on:changeChecked={changeChecked}
      {project}
      {experiment}
      {setList}
      {checked}
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
