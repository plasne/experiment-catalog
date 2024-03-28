<script lang="ts">
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";

  export let projectName: string;
  export let experiment: Experiment;

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let comparison: Comparison;
  let metrics: string[] = [];

  const fetchComparison = async () => {
    const response = await fetch(
      `${prefix}/api/projects/${projectName}/experiments/${experiment.name}/compare`
    );
    comparison = await response.json();
    const allKeys = [
      ...(comparison.lastResultForBaselineExperiment
        ? Object.keys(comparison.lastResultForBaselineExperiment.metrics)
        : []),
      ...(comparison.baselineResultForChosenExperiment
        ? Object.keys(comparison.baselineResultForChosenExperiment.metrics)
        : []),
      ...(comparison.lastResultForChosenExperiment
        ? Object.keys(comparison.lastResultForChosenExperiment.metrics)
        : []),
    ];
    metrics = [...new Set(allKeys)];
  };

  $: fetchComparison();
</script>

<table>
  <thead>
    <tr>
      <th></th>
      <th>
        <ComparisonTableHeader
          title="Project Baseline"
          result={comparison?.lastResultForBaselineExperiment}
        />
      </th>
      <th>
        <ComparisonTableHeader
          title="Experiment Baseline"
          result={comparison?.baselineResultForChosenExperiment}
        />
      </th>
      <th>
        <ComparisonTableHeader
          title="Last Experiment"
          result={comparison?.lastResultForChosenExperiment}
        />
      </th>
    </tr>
  </thead>
  <tbody>
    {#each metrics as metric}
      <tr>
        <td class="label">{metric}</td>
        <td
          ><ComparisonTableMetric
            result={comparison.lastResultForBaselineExperiment}
            {metric}
          /></td
        >
        <td
          ><ComparisonTableMetric
            result={comparison.baselineResultForChosenExperiment}
            baseline={comparison.lastResultForBaselineExperiment}
            {metric}
          /></td
        >
        <td
          ><ComparisonTableMetric
            result={comparison.lastResultForChosenExperiment}
            baseline={comparison.lastResultForBaselineExperiment}
            {metric}
          /></td
        >
      </tr>
    {/each}
  </tbody>
</table>

<style>
  table {
    width: 100%;
    border-collapse: collapse;
  }

  th {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
    border-bottom: 1px solid #ddd;
  }

  td {
    text-align: center;
  }

  td.label {
    text-align: left;
    font-weight: bold;
  }
</style>
