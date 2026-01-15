<script lang="ts">
  import { onMount } from "svelte";
  import ProjectCard from "./ProjectCard.svelte";
  import CreateProjectModal from "./CreateProjectModal.svelte";

  interface Props {
    onselect?: (project: Project) => void;
  }

  let { onselect }: Props = $props();

  let projects: Project[] = $state([]);
  let loadingState: "loading" | "loaded" | "error" = $state("loading");
  let showCreateModal = $state(false);
  let createError = $state("");

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";

  const fetchProjects = async () => {
    try {
      loadingState = "loading";
      const response = await fetch(`${prefix}/api/projects`, {
        credentials: "include",
      });
      projects = await response.json();
      loadingState = "loaded";
    } catch (error) {
      console.error(error);
      loadingState = "error";
    }
  };

  onMount(() => {
    fetchProjects();
  });

  const select = (project: Project) => {
    onselect?.(project);
  };

  const openCreateModal = () => {
    createError = "";
    showCreateModal = true;
  };

  const handleCreateSubmit = async (event: { name: string }) => {
    const { name } = event;
    createError = "";
    try {
      const response = await fetch(`${prefix}/api/projects`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ name }),
        credentials: "include",
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
  <button class="link" onclick={openCreateModal}>+ create project</button>
</div>

{#if loadingState === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
  </div>
{:else if loadingState === "error"}
  <div>Error loading experiments.</div>
{:else}
  <div class="flex-container">
    {#each projects as project (project.name)}
      <ProjectCard onselect={select} {project} />
    {/each}
  </div>
{/if}

<CreateProjectModal
  isOpen={showCreateModal}
  error={createError}
  onsubmit={handleCreateSubmit}
  oncancel={handleCreateCancel}
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
