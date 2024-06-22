<script lang="ts">
  import { createEventDispatcher } from "svelte";
  const dispatch = createEventDispatcher();

  export let metrics: string[];

  let buttonId = crypto.randomUUID();
  let filter: string;

  function apply() {
    if (!filter) {
      dispatch("filter", undefined);
      return;
    }

    var funcstr = filter.replace(/AND/gi, " && ").replace(/OR/gi, " || ");
    for (const metric of metrics) {
      funcstr = funcstr.replace(
        new RegExp(metric, "g"),
        `(result.metrics["${metric}"] ? result.metrics["${metric}"].value : null)`
      );
    }
    // Construct the function using the Function constructor
    var func = new Function(
      "result",
      `try { return ${funcstr}; } catch (e) { return false; }`
    );
    dispatch("filter", func);
  }
</script>

<div>
  <label for={buttonId}>filter:</label>
  <textarea id={buttonId} bind:value={filter} on:change={apply}></textarea>
</div>

<style>
  label {
    vertical-align: top;
  }
  textarea {
    width: 80em;
    height: 5em;
  }
</style>
