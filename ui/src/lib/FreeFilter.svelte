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

    var funcstr = filter.replace(/ AND /gi, " && ").replace(/ OR /gi, " || ");
    for (const metric of metrics) {
      funcstr = funcstr
        .replace(
          new RegExp(`baseline.${metric}`, "gi"),
          `(baseline.metrics["${metric}"] ? baseline.metrics["${metric}"].value : null)`,
        )
        .replace(
          new RegExp(`(?<!")${metric}`, "gi"),
          `(result.metrics["${metric}"] ? result.metrics["${metric}"].value : null)`,
        );
    }
    funcstr = funcstr.replace(/ref /gi, "result.ref");
    console.info(funcstr);

    var func = new Function(
      "baseline",
      "result",
      `try { return ${funcstr}; } catch (e) { console.warn("filter: " + e); return false; }`,
    );
    dispatch("filter", func);
  }

  function clear() {
    filter = "";
    apply();
  }
</script>

<div class="top">
  <label for={buttonId}>filter:</label>
  <textarea id={buttonId} bind:value={filter}></textarea>
  <button on:click={apply}>Apply</button>
  <button on:click={clear}>Clear</button>
</div>

<style>
  .top > * {
    vertical-align: top;
  }
  textarea {
    width: 60em;
    height: 5em;
  }
</style>
