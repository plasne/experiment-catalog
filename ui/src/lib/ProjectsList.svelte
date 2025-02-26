<!-- Copyright (c) Microsoft Corporation. -->
<!-- Licensed under the MIT license. -->

<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import ProjectCard from "./ProjectCard.svelte";

  let projects: Project[] = [];
  let state: "loading" | "loaded" | "error" = "loading";

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
</script>

<h1>Projects</h1>

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

<style>
  .flex-container {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-around;
  }
</style>
