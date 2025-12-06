<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import Annotations from "./Annotations.svelte";
  import SetSelector from "./SetSelector.svelte";
  import CreateAnnotationModal from "./CreateAnnotationModal.svelte";

  export let title: string;
  export let entity: ComparisonEntity;
  export let entities: ComparisonEntity[] = [];
  export let clickable: boolean = true;
  export let index: number = -1;

  const dispatch = createEventDispatcher();

  let showAnnotationModal = false;

  const drilldown = () => {
    if (entity?.set) dispatch("drilldown", entity.set);
  };

  const select = (event: CustomEvent<ComparisonEntity>) => {
    dispatch("select", { index, entity: event.detail });
  };

  const convertToFriendlyTime = (runtime: number): string => {
    if (!runtime) return "-";
    const hours = Math.floor(runtime / 3600);
    const minutes = Math.floor((runtime % 3600) / 60);
    const seconds = Math.round(runtime % 60);
    return `${hours}h ${minutes}m ${seconds}s`;
  };

  const openAnnotationModal = () => {
    showAnnotationModal = true;
  };

  const handleAnnotationSubmit = (event: CustomEvent<Annotation>) => {
    showAnnotationModal = false;
    dispatch("addAnnotation", {
      set: entity.set,
      annotation: event.detail,
      project: entity.project,
      experiment: entity.experiment,
    });
  };

  const handleAnnotationCancel = () => {
    showAnnotationModal = false;
  };
</script>

<div class="title">{title}</div>
<div class="set">
  {#if clickable}
    <button class="link" on:click={drilldown}>set:</button>
  {:else}
    <span>set: {entity?.set ?? "-"}</span>
  {/if}
  {#if entities.length > 0}
    <SetSelector {entity} {entities} on:select={select} />
  {/if}
</div>
<Annotations {entity} />
<div class="runtime-row">
  <span class="runtime">{convertToFriendlyTime(entity?.result?.runtime)}</span>
  {#if entity?.set}
    <button class="link add-annotation-link" on:click={openAnnotationModal}
      >+ annotation</button
    >
  {/if}
</div>

<CreateAnnotationModal
  isOpen={showAnnotationModal}
  setName={entity?.set ?? ""}
  on:submit={handleAnnotationSubmit}
  on:cancel={handleAnnotationCancel}
/>

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    gap: 0.25rem;
  }

  .runtime-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
  }

  .runtime {
    font-size: 0.6rem;
    color: #888;
  }

  .add-annotation-link {
    font-size: 0.6rem;
    color: #888;
  }

  .add-annotation-link:hover {
    color: #ccc;
  }
</style>
