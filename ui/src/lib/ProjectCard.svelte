<!-- Copyright (c) Microsoft Corporation. -->
<!-- Licensed under the MIT license. -->

<script lang="ts">
  import { createEventDispatcher } from "svelte";

  export let project: Project;

  const dispatch = createEventDispatcher();

  const select = () => {
    dispatch("select", project);
  };
</script>

<div class="card">
  <div class="title">
    <button class="link" on:click={select}>{project.name}</button>
  </div>
</div>

<style>
  .card {
    border: 1px solid #ccc;
    border-radius: 4px;
    padding: 1rem;
    margin: 1rem;
    max-width: 20rem;
  }

  .title {
    font-size: 1.5rem;
    font-weight: bold;
    color: #ccc;
  }
</style>
