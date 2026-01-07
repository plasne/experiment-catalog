<script lang="ts">
  import TriStateCheckbox from "./TriStateCheckbox.svelte";

  interface Props {
    options?: string[];
    yes?: Set<string>;
    no?: Set<string>;
  }

  let {
    options = [],
    yes = $bindable(new Set()),
    no = $bindable(new Set()),
  }: Props = $props();

  function change(event: {
    label: string;
    state: "include" | "exclude" | "neither";
  }) {
    const { label, state } = event;
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
    onchange={change}
  />
{/each}
