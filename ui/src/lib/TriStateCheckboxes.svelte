<script lang="ts">
  import TriStateCheckbox from "./TriStateCheckbox.svelte";

  export let options: string[] = [];
  export let yes: Set<string> = new Set();
  export let no: Set<string> = new Set();

  function change(event: CustomEvent) {
    const { label, state } = event.detail;
    switch (state) {
      case "include":
        yes.add(label);
        no.delete(label);
        break;
      case "exclude":
        no.add(label);
        yes.delete(label);
        break;
      case "neither":
        yes.delete(label);
        no.delete(label);
        break;
    }
    // Reassign to trigger Svelte reactivity
    yes = yes;
    no = no;
  }
</script>

{#each options as option}
  <TriStateCheckbox
    label={option}
    initialState={yes.has(option)
      ? "include"
      : no.has(option)
        ? "exclude"
        : "neither"}
    on:change={change}
  />
{/each}
