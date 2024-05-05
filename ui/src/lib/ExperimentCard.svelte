<script lang="ts">
  import { createEventDispatcher } from "svelte";

  export let experiment: Experiment;

  const dispatch = createEventDispatcher();

  const select = () => {
    dispatch("select", experiment);
  };
</script>

<div class="card">
  <div class="title">
    <button class="link" on:click={select}>{experiment.name}</button>
  </div>
  <div class="hypothesis"><b>Hypothesis:</b> {experiment.hypothesis}</div>
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

  .hypothesis {
    font-size: 1.2rem;
  }
</style>
