<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import Annotations from "./Annotations.svelte";

  export let details: SetDetails[] = [];
  export let selected: string = "";

  const dispatch = createEventDispatcher();
  let isOpen = false;

  const toggleDropdown = () => {
    isOpen = !isOpen;
  };

  const selectOption = (option: SetDetails) => {
    selected = option.name;
    isOpen = false;
    dispatch("select", option.name);
  };

  function clickOutside(node) {
    const handleClick = (event) => {
      if (!node.contains(event.target)) {
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

<div class="dropdown" use:clickOutside>
  <button data-set-options on:click={toggleDropdown}>{selected}</button>
  {#if isOpen}
    <div class="dropdown-menu">
      {#each details as option}
        <button on:click={() => selectOption(option)}>
          <div class="dropdown-item">
            <div class="title">{option.name}</div>
            {#each option.annotations as annotation}
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
  .dropdown {
    position: relative;
    display: inline-block;
    width: 14rem;
  }

  .title {
    font-size: 1.1rem;
    font-weight: bold;
  }

  .dropdown-menu {
    position: absolute;
    top: 100%;
    left: 0;
    width: 100%;
    border: 1px solid #ccc;
    background-color: #fff;
    z-index: 1000;
    max-height: 200px;
    overflow-y: auto;
  }

  .dropdown-item {
    padding: 10px;
    text-align: left;
    color: black;
    cursor: pointer;
  }
</style>
