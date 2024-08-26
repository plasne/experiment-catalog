<script lang="ts">
  import { onMount } from "svelte";
  import { createEventDispatcher } from "svelte";

  export let experiment: Experiment;

  let titleRef;
  let cardRef;

  const dispatch = createEventDispatcher();

  const select = () => {
    dispatch("select", experiment);
  };

  onMount(() => {
    if (titleRef && cardRef) {
      const titleWidth = titleRef.offsetWidth;
      cardRef.style.maxWidth = `${titleWidth}px`;
      titleRef.style.display = "none";
    }
  });
</script>

<div class="title" bind:this={titleRef}>
  <button class="link" on:click={select}>{experiment.name}</button>
</div>

<div class="card" bind:this={cardRef}>
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
    min-width: 20rem;
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
