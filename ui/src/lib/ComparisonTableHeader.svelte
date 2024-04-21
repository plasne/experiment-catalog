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
  <button class="link-button" on:click={select}
    >set: {result?.set ?? "-"}</button
  >
</div>
<div class="desc">
  <span>desc:</span>
  {result?.description ?? "-"}
</div>

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
  }

  .desc {
    font-size: 0.8rem;
  }

  .link-button {
    background: none;
    border: none;
    cursor: pointer;
    padding: 0;
    font-size: inherit;
    font-weight: inherit;
    color: inherit;
    text-decoration: underline;
    text-decoration-color: #777;
  }

  .link-button:hover {
    text-decoration-color: #ccc;
  }
</style>
