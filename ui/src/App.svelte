<script lang="ts">
  import {
    loadExperiment,
    updateURL,
    decodeConfig,
    type ViewConfig,
  } from "./lib/Tools";
  import ExperimentsList from "./lib/ExperimentsList.svelte";
  import ExperimentPage from "./lib/ExperimentPage.svelte";
  import SetPage from "./lib/SetPage.svelte";
  import ProjectsList from "./lib/ProjectsList.svelte";
  import { onMount } from "svelte";

  let loadingState: "loading" | "loaded" | "error" = $state("loading");
  let project: Project = $state();
  let experiment: Experiment = $state();
  let setList: string = $state();
  let setName: string = $state();
  let config: ViewConfig = $state({});

  const selectProject = (selectedProject: Project) => {
    project = selectedProject;
    updateURL(project.name);
  };

  const unselectProject = () => {
    project = undefined;
    setList = undefined;
    updateURL();
  };

  const selectExperiment = (selectedExperiment: Experiment) => {
    experiment = selectedExperiment;
    updateURL(project.name, experiment.name);
  };

  const unselectExperiment = () => {
    experiment = undefined;
    setList = undefined;
    updateURL(project.name);
  };

  const selectSet = (selectedSet: string) => {
    setName = selectedSet;
    updateURL(project.name, experiment.name, `set:${setName}`, config);
  };

  const unselectSet = () => {
    setName = undefined;
    updateURL(
      project.name,
      experiment.name,
      setList ? `sets:${setList}` : null,
      config
    );
  };

  const changeSetList = (newSetList: string) => {
    setList = newSetList;
    updateURL(project.name, experiment.name, `sets:${setList}`, config);
  };

  const changeConfig = (newConfig: ViewConfig) => {
    config = newConfig;
    if (setName) {
      updateURL(project.name, experiment.name, `set:${setName}`, config);
    } else if (setList) {
      updateURL(project.name, experiment.name, `sets:${setList}`, config);
    } else {
      updateURL(project.name, experiment.name, null, config);
    }
  };

  async function parseQueryString() {
    try {
      const params = new URLSearchParams(window.location.search);
      const qproject = params.get("project");
      const qexperiment = params.get("experiment");
      const qpage = params.get("page");
      const qconfig = params.get("config");
      const qchecked = params.get("checked"); // backward compatibility

      // Parse config from URL or fall back to legacy "checked" param
      if (qconfig) {
        config = decodeConfig(qconfig);
      } else if (qchecked) {
        config = { checked_metrics: qchecked };
      } else {
        config = {};
      }

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
      config = {};
    }

    loadingState = "loaded";
  }

  onMount(parseQueryString);
</script>

<main>
  {#if loadingState === "loading"}
    <div>Loading...</div>
    <div>
      <img class="loading" alt="loading" src="/spinner.gif" />
    </div>
  {:else if project && experiment && setName}
    <SetPage
      onunselectSet={unselectSet}
      onchangeConfig={changeConfig}
      {project}
      {experiment}
      {setName}
      {config}
    />
  {:else if project && experiment}
    <ExperimentPage
      onunselectExperiment={unselectExperiment}
      onselectSet={selectSet}
      onchangeSetList={changeSetList}
      onchangeConfig={changeConfig}
      {project}
      {experiment}
      {setList}
      {config}
    />
  {:else if project}
    <ExperimentsList
      onselect={selectExperiment}
      onunselectProject={unselectProject}
      {project}
    />
  {:else}
    <ProjectsList onselect={selectProject} />
  {/if}
</main>
