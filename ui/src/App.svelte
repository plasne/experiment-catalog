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

  let state: "loading" | "loaded" | "error" = "loading";
  let project: Project;
  let experiment: Experiment;
  let setList: string;
  let setName: string;
  let config: ViewConfig = {};

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
    updateURL(project.name, experiment.name, `set:${setName}`, config);
  };

  const unselectSet = () => {
    setName = undefined;
    updateURL(project.name, experiment.name);
  };

  const changeSetList = (event: CustomEvent<string>) => {
    setList = event.detail;
    updateURL(project.name, experiment.name, `sets:${setList}`, config);
  };

  const changeConfig = (event: CustomEvent<ViewConfig>) => {
    config = event.detail;
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
    <SetPage
      on:unselectSet={unselectSet}
      on:changeConfig={changeConfig}
      {project}
      {experiment}
      {setName}
      {config}
    />
  {:else if project && experiment}
    <ExperimentPage
      on:unselectExperiment={unselectExperiment}
      on:selectSet={selectSet}
      on:changeSetList={changeSetList}
      on:changeConfig={changeConfig}
      {project}
      {experiment}
      {setList}
      {config}
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
