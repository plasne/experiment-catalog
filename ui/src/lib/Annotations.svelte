<script lang="ts">
  interface Props {
    entity?: ComparisonEntity | null;
  }

  let { entity }: Props = $props();

  const openAnnotationUri = (uri: string) => {
    window.open(uri, "_blank", "noopener,noreferrer");
  };
</script>

{#if entity && entity.result?.annotations}
  {#each entity.result?.annotations as annotation}
    <div class="annotation">
      {#if annotation.uri}
        <span>{annotation.text}</span>
        <button
          type="button"
          class="annotation-action"
          onclick={() => openAnnotationUri(annotation.uri!)}
        >
          Open link
        </button>
      {:else}
        {annotation.text}
      {/if}
    </div>
  {/each}
{/if}

<style>
  .annotation {
    font-size: 0.8rem;
    color: black;
    background-color: yellow;
    margin: 0.2rem;
    padding: 0.2rem;
  }

  .annotation-action {
    margin-left: 0.4rem;
  }
</style>
