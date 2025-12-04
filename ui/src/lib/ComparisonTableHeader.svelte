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

  const convertToFriendlyTime = (runtime: number): string => {
    if (!runtime) return "-";
    const hours = Math.floor(runtime / 3600);
    const minutes = Math.floor((runtime % 3600) / 60);
    const seconds = Math.round(runtime % 60);
    return `${hours}h ${minutes}m ${seconds}s`;
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
<div class="runtime">{convertToFriendlyTime(result?.runtime)}</div>

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    gap: 0.25rem;
  }

  .runtime {
    font-size: 0.6rem;
    color: #888;
    text-align: right;
  }
</style>
