<script lang="ts">
  export let metricDefinitions: MetricDefinition[] = [];
  export let selectedMetrics: string[] = [];

  let isCollapsed = true;

  // Extract prefix from metric name (before first _ or -)
  function getPrefix(name: string): string {
    const match = name.match(/^([^_-]+)/);
    return match ? match[1] : name;
  }

  // Group metrics by prefix
  $: prefixes = (() => {
    const map = new Map<string, string[]>();
    for (const def of metricDefinitions) {
      const prefix = getPrefix(def.name);
      if (!map.has(prefix)) {
        map.set(prefix, []);
      }
      map.get(prefix)!.push(def.name);
    }
    return map;
  })();

  // Initialize selectedMetrics based on count
  $: if (metricDefinitions.length > 0 && selectedMetrics.length === 0) {
    if (metricDefinitions.length <= 10) {
      selectedMetrics = metricDefinitions.map((d) => d.name);
    }
  }

  function toggleMetric(metric: string) {
    if (selectedMetrics.includes(metric)) {
      selectedMetrics = selectedMetrics.filter((m) => m !== metric);
    } else {
      selectedMetrics = [...selectedMetrics, metric];
    }
  }

  function togglePrefix(prefix: string) {
    const metricsInPrefix = prefixes.get(prefix) || [];
    const anyChecked = metricsInPrefix.some((m) => selectedMetrics.includes(m));

    if (anyChecked) {
      // Uncheck all metrics in this prefix
      selectedMetrics = selectedMetrics.filter(
        (m) => !metricsInPrefix.includes(m)
      );
    } else {
      // Check all metrics in this prefix
      const newSelected = new Set(selectedMetrics);
      for (const m of metricsInPrefix) {
        newSelected.add(m);
      }
      selectedMetrics = [...newSelected];
    }
  }
</script>

<div class="metrics-filter">
  {#if metricDefinitions.length > 10}
    <button class="link" on:click={() => (isCollapsed = !isCollapsed)}>
      metrics ({selectedMetrics.length}/{metricDefinitions.length})
    </button>
  {:else}
    <div>metrics ({selectedMetrics.length}/{metricDefinitions.length})</div>
  {/if}
  {#if metricDefinitions.length <= 10 || !isCollapsed}
    <div class="prefix-groups">
      {#each [...prefixes.entries()] as [prefix, metricsInPrefix]}
        <div class="prefix-group">
          <button
            class="link prefix-label"
            on:click={() => togglePrefix(prefix)}>{prefix}</button
          >
          <div class="metrics-list">
            {#each metricsInPrefix as metric}
              <nobr>
                <input
                  type="checkbox"
                  checked={selectedMetrics.includes(metric)}
                  on:change={() => toggleMetric(metric)}
                  id="metric-{metric}"
                />
                <label for="metric-{metric}">{metric}</label>
              </nobr>
            {/each}
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>

<style>
  .metrics-filter {
    display: flex;
    flex-direction: row;
    flex-wrap: wrap;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .prefix-groups {
    display: flex;
    flex-direction: row;
    flex-wrap: wrap;
    gap: 1rem;
  }

  .prefix-group {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding: 0.5rem;
    border: 1px solid #555;
    border-radius: 4px;
  }

  .prefix-label {
    font-weight: bold;
  }

  .metrics-list {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    margin-left: 1rem;
  }

  input[type="checkbox"] {
    cursor: pointer;
    margin-right: 0.5rem;
  }

  label {
    cursor: pointer;
  }
</style>
