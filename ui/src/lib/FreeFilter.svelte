<script lang="ts">
  interface Props {
    metrics: string[];
    filteredCount?: number;
    totalCount?: number;
    onfilter?: (func: Function | undefined) => void;
  }

  let {
    metrics,
    filteredCount = 0,
    totalCount = 0,
    onfilter,
  }: Props = $props();

  let buttonId = crypto.randomUUID();
  let filter: string = $state("");

  function apply() {
    if (!filter) {
      onfilter?.(undefined);
      return;
    }

    var funcstr = filter.replace(/ AND /gi, " && ").replace(/ OR /gi, " || ");
    const sorted = [...metrics].sort((a, b) => b.length - a.length);
    for (const metric of sorted) {
      funcstr = funcstr
        .replace(
          new RegExp(`\\[baseline.${metric}\\]`, "gi"),
          `(baseline.metrics["${metric}"] ? baseline.metrics["${metric}"].value : undefined)`
        )
        .replace(
          new RegExp(`\\[${metric}\\]`, "gi"),
          `(result.metrics["${metric}"] ? result.metrics["${metric}"].value : undefined)`
        );
    }
    funcstr = funcstr.replace(/ref /gi, "result.ref");
    console.info(funcstr);

    var func = new Function(
      "baseline",
      "result",
      `try { return ${funcstr}; } catch (e) { console.warn("filter: " + e); return false; }`
    );
    onfilter?.(func);
  }

  function clear() {
    filter = "";
    apply();
  }
</script>

<div class="top">
  <label for={buttonId}>filter:</label>
  <textarea id={buttonId} bind:value={filter}></textarea>
  <button onclick={apply}>Apply</button>
  <button onclick={clear}>Clear</button>
  <span class="count">{filteredCount} of {totalCount}</span>
</div>

<style>
  .top > * {
    vertical-align: top;
  }
  textarea {
    width: 60em;
    height: 5em;
  }
  .count {
    margin-left: 1rem;
    font-style: italic;
  }
</style>
