<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import Annotations from "./Annotations.svelte";
  import SetSelector from "./SetSelector.svelte";

  export let title: string;
  export let result: Result;
  export let details: SetDetails[] = [];
  export let clickable: boolean = true;

  const dispatch = createEventDispatcher();

  const select = () => {
    if (result?.set) dispatch("selectSet", result.set);
  };

  const onChangeSet = () => {
    dispatch("changeSet");
  };
</script>

<div class="title">{title}</div>
<div class="set">
  {#if clickable}
    <button class="link" on:click={select}>set:</button>
  {:else}
    <span>set: {result?.set ?? "-"}</span>
  {/if}
  {#if details.length > 0}
    <SetSelector {details} on:change={onChangeSet} />
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
