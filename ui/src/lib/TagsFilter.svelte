<script lang="ts">
  import TriStateCheckboxes from "./TriStateCheckboxes.svelte";

  export let project: Project;
  export let querystring: string;

  let prefix =
    window.location.hostname === "localhost" ? "http://localhost:6010" : "";
  let tags: string[] = [];
  let yes: Set<string>;
  let no: Set<string>;

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
    ].join("&");
  }

  $: fetchTags();
</script>

<div class="checkbox-container">
  <span>filter by tags:</span>
  <TriStateCheckboxes bind:yes bind:no options={tags} />
  <button on:click={apply}>apply</button>
</div>

<style>
  .checkbox-container {
    display: flex;
    flex-direction: row;
  }
  button {
    margin-left: 1.2em;
    cursor: pointer;
  }
</style>
