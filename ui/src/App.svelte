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
  let authRequired: boolean = $state(false);
  let project: Project = $state();
  let experiment: Experiment = $state();
  let setList: string = $state();
  let setName: string = $state();
  let config: ViewConfig = $state({});

  function getCookie(name: string): string | null {
    const match = document.cookie.match(new RegExp(`(^| )${name}=([^;]+)`));
    return match ? match[2] : null;
  }

  function setCookie(name: string, value: string, days: number = 1): void {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${name}=${value}; expires=${expires}; path=/`;
  }

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";

  async function checkAuth(): Promise<boolean> {
    // If we've previously determined auth is not required, skip the check
    if (getCookie("auth_not_required") === "true") {
      return true;
    }

    // Check with the server if auth is required
    try {
      const response = await fetch(`${prefix}/auth/status`, {
        credentials: "include",
      });
      const data = await response.json();
      if (data.username) {
        // User is authenticated, no need to show login
        return true;
      }
      if (data.is_required) {
        authRequired = true;
        return false;
      } else {
        // Auth is not required, cache this so we don't call again
        setCookie("auth_not_required", "true", 1);
        return true;
      }
    } catch {
      // If we can't reach the auth endpoint, assume auth is not required
      return true;
    }
  }

  function login(): void {
    window.location.href = `${prefix}/auth/login?return-url=${encodeURIComponent(
      window.location.href
    )}`;
  }

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
    // Check authentication first
    const authSatisfied = await checkAuth();
    if (!authSatisfied) {
      loadingState = "loaded";
      return;
    }

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
  {:else if authRequired}
    <div class="auth-container">
      <h2>Authentication Required</h2>
      <button onclick={login}>Login</button>
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
