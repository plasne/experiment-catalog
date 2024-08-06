<script lang="ts">
  import { createEventDispatcher } from "svelte";

  export let result: Result;
  export let results: Result[] = [];

  const dispatch = createEventDispatcher();
  let isOpen = false;

  const toggleDropdown = () => {
    isOpen = !isOpen;
  };

  const select = (selected: Result) => {
    result = selected;
    isOpen = false;
    dispatch("select", selected);
  };

  function clickOutside(node: HTMLElement) {
    const handleClick = (event: MouseEvent) => {
      if (!node.contains(event.target as Node)) {
        isOpen = false;
      }
    };

    document.addEventListener("click", handleClick, true);

    return {
      destroy() {
        document.removeEventListener("click", handleClick, true);
      },
    };
  }
</script>

<div class="dropdown-container" use:clickOutside>
  <button class="dropdown-header" on:click={toggleDropdown}
    >{result ? result.set : "None"}</button
  >
  {#if isOpen}
    <div class="dropdown-menu">
      <button class="dropdown-button" on:click={() => select(null)}>
        <div class="dropdown-item">
          <div class="title">None</div>
        </div>
      </button>
      {#each results as result}
        <button class="dropdown-button" on:click={() => select(result)}>
          <div class="dropdown-item">
            <div class="title">{result.set}</div>
            {#each result.annotations as annotation}
              <div>
                {annotation.text}
              </div>
            {/each}
          </div>
        </button>
      {/each}
    </div>
  {/if}
</div>

<style>
  .dropdown-container {
    position: relative;
    display: inline-block;
    width: 14rem;
  }

  .dropdown-header {
    width: 100%;
    text-align: left;
    border: 1px solid #ccc;
    border-radius: 0.15rem;
    font-size: 1rem;
    cursor: pointer;
  }

  .dropdown-menu {
    position: absolute;
    top: 100%;
    left: 0;
    z-index: 1000;
    max-height: 14rem;
    overflow-y: auto;
  }

  .dropdown-button {
    width: 100%;
    text-align: left;
    background-color: #ccc;
  }

  .dropdown-item {
    text-align: left;
    color: black;
    cursor: pointer;
  }

  .title {
    font-size: 1.1rem;
    font-weight: bold;
  }
</style>
