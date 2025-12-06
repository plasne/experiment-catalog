<script lang="ts">
  import TriStateCheckboxes from "./TriStateCheckboxes.svelte";

  export let project: Project;
  export let querystring: string;

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let tags: string[] = [];
  let yes: Set<string> = new Set();
  let no: Set<string> = new Set();
  let isCollapsed = true;

  // Parse querystring to initialize yes/no sets
  function parseQuerystring() {
    yes = new Set();
    no = new Set();
    if (!querystring) return;

    const params = new URLSearchParams(querystring);
    const includeTags = params.get("include-tags");
    const excludeTags = params.get("exclude-tags");

    if (includeTags) {
      for (const tag of includeTags.split(",")) {
        if (tag) yes.add(tag);
      }
    }
    if (excludeTags) {
      for (const tag of excludeTags.split(",")) {
        if (tag) no.add(tag);
      }
    }
  }

  // Parse on initial load
  parseQuerystring();

  const fetchTags = async () => {
    try {
      const response = await fetch(
        `${prefix}/api/projects/${project.name}/tags`
      );
      tags = await response.json();
    } catch (error) {
      console.error(error);
    }
  };

  function apply() {
    querystring = [
      yes.size > 0 ? `include-tags=${[...yes].join(",")}` : "",
      no.size > 0 ? `exclude-tags=${[...no].join(",")}` : "",
    ]
      .filter((s) => s)
      .join("&");
  }

  $: selectedCount = (yes?.size || 0) + (no?.size || 0);

  $: fetchTags();
</script>

<div class="checkbox-container">
  {#if tags.length > 10}
    <button class="link" on:click={() => (isCollapsed = !isCollapsed)}>
      tags ({selectedCount}/{tags.length})
    </button>
  {:else}
    <span>tags ({selectedCount}/{tags.length})</span>
  {/if}
  {#if tags.length <= 10 || !isCollapsed}
    <TriStateCheckboxes bind:yes bind:no options={tags} />
    <button class="apply" on:click={apply}>apply</button>
  {/if}
</div>

<style>
  .checkbox-container {
    display: flex;
    flex-direction: row;
    flex-wrap: wrap;
    align-items: flex-start;
  }
  button.apply {
    margin-left: 0.5rem;
    cursor: pointer;
  }
</style>
