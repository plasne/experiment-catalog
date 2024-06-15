<script lang="ts">
  import { createEventDispatcher } from "svelte";
  const dispatch = createEventDispatcher();

  export let label: string;

  let state: "include" | "exclude" | "neither" = "neither";
  let buttonId = crypto.randomUUID();

  function toggle() {
    switch (state) {
      case "neither":
        state = "include";
        break;
      case "include":
        state = "exclude";
        break;
      case "exclude":
        state = "neither";
        break;
    }
    dispatch("change", { label, state });
  }
</script>

<button id={buttonId} class="checkbox-wrapper {state}" on:click={toggle}
></button>
<label for={buttonId}>{label}</label>

<style>
  .checkbox-wrapper {
    display: inline-block;
    width: 1.7em;
    height: 1.7em;
    background-color: var(--color-neither);
    border: solid 0.05em white;
    margin-left: 1.2em;
    margin-right: 0.5em;
  }
  .include::before {
    color: white;
    content: "✓";
  }
  .exclude::before {
    color: white;
    content: "✕";
  }
  .neither {
    background-color: transparent;
  }
</style>
