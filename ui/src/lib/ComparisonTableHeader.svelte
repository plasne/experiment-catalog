<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import Annotations from "./Annotations.svelte";
  import SetSelector from "./SetSelector.svelte";

  export let title: string;
  export let result: Result;
  export let results: Result[] = [];
  export let clickable: boolean = true;
  export let index: number = -1;

  const dispatch = createEventDispatcher();

  const drilldown = () => {
    if (result?.set) dispatch("drilldown", result.set);
  };

  const select = (event: CustomEvent<Result>) => {
    dispatch("select", { index, result: event.detail });
  };
</script>

<div class="title">{title}</div>
<div class="set">
  {#if clickable}
    <button class="link" on:click={drilldown}>set:</button>
  {:else}
    <span>set: {result?.set ?? "-"}</span>
  {/if}
  {#if results.length > 0}
    <SetSelector {result} {results} on:select={select} />
  {/if}
</div>
<Annotations {result} />

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
  }
</style>
