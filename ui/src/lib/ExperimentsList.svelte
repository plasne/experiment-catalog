<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ExperimentCard from "./ExperimentCard.svelte";
  import CreateExperimentModal from "./CreateExperimentModal.svelte";

  export let project: Project;
  let experiments: Experiment[] = [];
  let state: "loading" | "loaded" | "error" = "loading";
  let showCreateModal = false;
  let createError = "";

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  const dispatch = createEventDispatcher();

  const fetchExperiments = async () => {
    try {
      state = "loading";
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/experiments`
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

  const unselectProject = () => {
    dispatch("unselectProject");
  };

  const openCreateModal = () => {
    createError = "";
    showCreateModal = true;
  };

  const handleCreateSubmit = async (
    event: CustomEvent<{ name: string; hypothesis: string }>
  ) => {
    const { name, hypothesis } = event.detail;
    createError = "";
    try {
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/experiments`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ name, hypothesis }),
        }
      );
      if (response.ok) {
        showCreateModal = false;
        fetchExperiments();
      } else {
        const errorText = await response.text();
        createError =
          errorText || `Error: ${response.status} ${response.statusText}`;
      }
    } catch (error) {
      createError =
        error instanceof Error ? error.message : "An unexpected error occurred";
    }
  };

  const handleCreateCancel = () => {
    showCreateModal = false;
  };
</script>

<button class="link" on:click={unselectProject}>back</button>
<h1>Experiments in {project.name}</h1>
<div class="actions">
  <button class="link" on:click={openCreateModal}>+ create experiment</button>
</div>

{#if state === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
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

<CreateExperimentModal
  isOpen={showCreateModal}
  projectName={project.name}
  error={createError}
  on:submit={handleCreateSubmit}
  on:cancel={handleCreateCancel}
/>

<style>
  .actions {
    margin-bottom: 1rem;
  }

  .flex-container {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-around;
  }
</style>
