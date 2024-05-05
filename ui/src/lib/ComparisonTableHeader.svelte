<script lang="ts">
  import { createEventDispatcher } from "svelte";

  export let title: string;
  export let result: Result;

  const dispatch = createEventDispatcher();

  const select = () => {
    if (result?.set) dispatch("selectSet", result.set);
  };
</script>

<div class="title">{title}</div>
<div class="set">
  <button class="link" on:click={select}>set: {result?.set ?? "-"}</button>
</div>
{#if result?.annotations}
  {#each result?.annotations as annotation}
    <div class="annotation">
      {#if annotation.uri}
        <a class="link" href={annotation.uri} target="_blank"
          >{annotation.text}</a
        >
      {:else}
        {annotation.text}
      {/if}
    </div>
  {/each}
{/if}

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
  }

  .annotation {
    font-size: 0.8rem;
    color: black;
    background-color: yellow;
    margin: 0.2rem;
    padding: 0.2rem;
  }
</style>
