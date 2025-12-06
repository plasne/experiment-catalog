<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import ComparisonTableHeader from "./ComparisonTableHeader.svelte";
  import ComparisonTableMetric from "./ComparisonTableMetric.svelte";
  import TagsFilter from "./TagsFilter.svelte";
  import { sortMetrics } from "./Tools";

  export let project: Project;
  export let experiment: Experiment;
  export let setList: string;
  export let checked: string;
  export let initialTags: string = "";

  let state: "loading" | "loaded" | "error" = "loading";
  let compareCount = 3;
  let controls = [];
  let selected: ComparisonEntity[];
  let metricsHighlighted: Set<string>;

  const dispatch = createEventDispatcher();

  const drilldown = (event: CustomEvent<string>) => {
    dispatch("drilldown", event.detail);
  };

  const updateSetList = () => {
    if (!selected) return;
    setList = selected.map((result) => result?.set).join(",");
    dispatch("changeSetList", setList);
  };

  const toggleRowCheck = (metric: string) => {
    if (!metricsHighlighted) {
      metricsHighlighted = new Set<string>([metric]);
    } else if (metricsHighlighted.has(metric)) {
      metricsHighlighted.delete(metric);
      metricsHighlighted = metricsHighlighted; // trigger reactivity
    } else {
      metricsHighlighted.add(metric);
      metricsHighlighted = metricsHighlighted; // trigger reactivity
    }
    checked = Array.from(metricsHighlighted).join(",");
    dispatch("changeChecked", checked);
  };

  const applySetList = () => {
    selected = [];
    if (setList) {
      var setListSplit = setList.split(",");
      while (
        setListSplit.length > 0 &&
        setListSplit[setListSplit.length - 1].trim() === ""
      ) {
        setListSplit.pop();
      }
      for (var i = 0; i < Math.max(compareCount, setListSplit.length); i++) {
        const result =
          i < setListSplit.length
            ? comparison.sets.find((result) => result.set === setListSplit[i])
            : null;
        selected[i] = result;
      }
    } else {
      selected = comparison.sets?.slice(-compareCount);
    }
    updateSetList();
  };

  const select = (
    event: CustomEvent<{ index: number; entity: ComparisonEntity }>
  ) => {
    selected[event.detail.index] = event.detail.entity;
    updateSetList();
  };

  const addAnnotation = async (
    event: CustomEvent<{
      set: string;
      annotation: Annotation;
      project: string;
      experiment: string;
    }>
  ) => {
    const { set, annotation, project: proj, experiment: exp } = event.detail;
    try {
      const response = await fetch(
        `${prefix}/api/projects/${proj}/experiments/${exp}/results`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            set,
            annotations: [annotation],
          }),
        }
      );
      if (response.ok) {
        fetchComparison();
      } else {
        console.error("Failed to add annotation:", response.statusText);
      }
    } catch (error) {
      console.error("Failed to add annotation:", error);
    }
  };

  const selectLastSets = () => {
    if (!comparison?.sets) return;
    selected = comparison.sets.slice(-compareCount);
    updateSetList();
  };

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let comparison: Comparison;
  let metrics: string[] = [];
  let tagFilters: string = initialTags;
  let initialized = false;

  // Emit tag changes after initialization
  $: if (initialized && tagFilters !== undefined) {
    dispatch("changeTags", tagFilters);
  }

  const fetchComparison = async () => {
    try {
      state = "loading";

      // fetch comparison
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/experiments/${experiment.name}/compare?${tagFilters ?? ""}`
      );
      comparison = await response.json();

      // get a list of metrics
      const allKeys = [
        ...(comparison.project_baseline
          ? Object.keys(comparison.project_baseline?.result?.metrics)
          : []),
        ...(comparison.experiment_baseline
          ? Object.keys(comparison.experiment_baseline?.result?.metrics)
          : []),
        ...(comparison.sets
          ? comparison.sets?.flatMap((experiment) =>
              Object.keys(experiment.result?.metrics)
            )
          : []),
      ];
      metrics = [...new Set(allKeys)].sort((a, b) =>
        sortMetrics(comparison.metric_definitions, a, b)
      );

      // apply the set list
      applySetList();

      // apply the checked metrics
      if (checked) {
        metricsHighlighted = new Set(checked.split(","));
      }

      initialized = true;
      state = "loaded";
    } catch (error) {
      console.error(error);
      state = "error";
    }
  };

  export function reload() {
    fetchComparison();
  }

  $: fetchComparison(), tagFilters;
</script>

{#if comparison}
  <div class="selection">
    <TagsFilter {project} bind:querystring={tagFilters} />
    <span>show at least</span>
    <select bind:value={compareCount} on:change={applySetList}>
      <option value={1}>1</option>
      <option value={2}>2</option>
      <option value={3}>3</option>
      <option value={4}>4</option>
      <option value={5}>5</option>
      <option value={6}>6</option>
      <option value={7}>7</option>
      <option value={8}>8</option>
      <option value={9}>9</option>
      <option value={10}>10</option>
      <option value={20}>20</option>
      <option value={30}>30</option>
      <option value={100}>100</option>
    </select>
    <span>of {comparison.sets?.length} permutations</span>
    <button class="link" on:click={selectLastSets}>(show last)</button>
  </div>
{/if}

{#if state === "loading"}
  <div>Loading...</div>
  <div>
    <img class="loading" alt="loading" src="/spinner.gif" />
  </div>
{:else if state === "error"}
  <div>Error loading comparison.</div>
{:else if comparison}
  <table>
    <thead>
      <tr>
        <th class="checkbox-column"></th>
        <th></th>
        <th>
          <ComparisonTableHeader
            title="Project Baseline"
            entity={comparison.project_baseline}
            clickable={false}
            on:addAnnotation={addAnnotation}
          />
        </th>
        <th>
          <ComparisonTableHeader
            title="Experiment Baseline"
            entity={comparison.experiment_baseline}
            clickable={false}
            on:addAnnotation={addAnnotation}
          />
        </th>
        {#each selected as entity, index}
          <th>
            <ComparisonTableHeader
              {index}
              title=""
              {entity}
              entities={comparison.sets}
              on:drilldown={drilldown}
              on:select={select}
              on:addAnnotation={addAnnotation}
            />
          </th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each metrics as metric}
        <tr class:highlighted={metricsHighlighted?.has(metric)}>
          <td class="checkbox-column">
            <input
              type="checkbox"
              checked={metricsHighlighted?.has(metric)}
              on:change={() => toggleRowCheck(metric)}
            />
          </td>
          <td class="label">{metric}</td>
          <td
            ><ComparisonTableMetric
              result={comparison.project_baseline?.result}
              baseline={comparison.experiment_baseline?.result}
              {metric}
              definition={comparison.metric_definitions[metric]}
            /></td
          >
          <td
            ><ComparisonTableMetric
              result={comparison.experiment_baseline?.result}
              {metric}
              definition={comparison.metric_definitions[metric]}
            /></td
          >
          {#each selected as entity, index}
            <td
              ><ComparisonTableMetric
                bind:this={controls[index]}
                result={entity?.result}
                baseline={comparison.experiment_baseline?.result}
                {metric}
                definition={comparison.metric_definitions[metric]}
                pvalue={entity?.p_values?.[metric]?.value}
              /></td
            >
          {/each}
        </tr>
      {/each}
    </tbody>
  </table>
{/if}

<style>
  table {
    width: 100%;
    border-collapse: collapse;
  }

  th {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
    vertical-align: bottom;
    border-bottom: 1px solid #ddd;
  }

  td {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
    text-align: left;
  }

  td.label {
    text-align: left;
    font-weight: bold;
  }

  .checkbox-column {
    text-align: center;
  }

  .highlighted {
    background-color: #333333;
  }

  input[type="checkbox"] {
    cursor: pointer;
  }

  .selection {
    width: 80rem;
    text-align: right;
  }
</style>
