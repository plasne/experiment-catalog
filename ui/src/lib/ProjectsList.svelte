<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ProjectCard from "./ProjectCard.svelte";
  import CreateProjectModal from "./CreateProjectModal.svelte";

  let projects: Project[] = [];
  let state: "loading" | "loaded" | "error" = "loading";
  let showCreateModal = false;
  let createError = "";

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  const dispatch = createEventDispatcher();

  const fetchProjects = async () => {
    try {
      state = "loading";
      const response = await fetch(`${prefix}/api/projects`);
      projects = await response.json();
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  $: fetchProjects();

  const select = (event: CustomEvent<Project>) => {
    dispatch("select", event.detail);
  };

  const openCreateModal = () => {
    createError = "";
    showCreateModal = true;
  };

  const handleCreateSubmit = async (event: CustomEvent<{ name: string }>) => {
    const { name } = event.detail;
    createError = "";
    try {
      const response = await fetch(`${prefix}/api/projects`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ name }),
      });
      if (response.ok) {
        showCreateModal = false;
        fetchProjects();
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

<h1>Projects</h1>
<div class="actions">
  <button class="link" on:click={openCreateModal}>+ create project</button>
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
    {#each projects as project (project.name)}
      <ProjectCard on:select={select} {project} />
    {/each}
  </div>
{/if}

<CreateProjectModal
  isOpen={showCreateModal}
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
