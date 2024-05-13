<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import Annotations from "./Annotations.svelte";

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
