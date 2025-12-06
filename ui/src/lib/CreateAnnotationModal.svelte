<script lang="ts">
  import { createEventDispatcher } from "svelte";

  export let isOpen = false;
  export let setName: string = "";

  const dispatch = createEventDispatcher();

  let annotationText = "";
  let annotationUri = "";

  const handleSubmit = () => {
    if (!annotationText.trim()) return;

    const annotation: Annotation = {
      text: annotationText.trim(),
    };

    if (annotationUri.trim()) {
      annotation.uri = annotationUri.trim();
    }

    dispatch("submit", annotation);
    resetForm();
  };

  const handleCancel = () => {
    dispatch("cancel");
    resetForm();
  };

  const resetForm = () => {
    annotationText = "";
    annotationUri = "";
  };

  const handleKeydown = (event: KeyboardEvent) => {
    if (event.key === "Escape") {
      handleCancel();
    }
  };
</script>

<svelte:window on:keydown={handleKeydown} />

{#if isOpen}
  <!-- svelte-ignore a11y-click-events-have-key-events a11y-no-noninteractive-element-interactions -->
  <div class="modal-backdrop" on:click={handleCancel} role="presentation">
    <!-- svelte-ignore a11y-click-events-have-key-events a11y-no-noninteractive-element-interactions -->
    <div
      class="modal"
      on:click|stopPropagation
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <h3 id="modal-title">Add Annotation to Set: {setName}</h3>

      <div class="form-group">
        <label for="annotation-text">Annotation Text *</label>
        <input
          id="annotation-text"
          type="text"
          bind:value={annotationText}
          placeholder="Enter annotation text..."
        />
      </div>

      <div class="form-group">
        <label for="annotation-uri">Link URL (optional)</label>
        <input
          id="annotation-uri"
          type="url"
          bind:value={annotationUri}
          placeholder="https://..."
        />
      </div>

      <div class="button-group">
        <button class="cancel-btn" on:click={handleCancel}>Cancel</button>
        <button
          class="submit-btn"
          on:click={handleSubmit}
          disabled={!annotationText.trim()}
        >
          Add Annotation
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  .modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.7);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1000;
  }

  .modal {
    background-color: #2a2a2a;
    border: 1px solid #444;
    border-radius: 8px;
    padding: 1.5rem;
    min-width: 400px;
    max-width: 90%;
  }

  h3 {
    margin: 0 0 1rem 0;
    color: #fff;
    font-size: 1.1rem;
  }

  .form-group {
    margin-bottom: 1rem;
  }

  label {
    display: block;
    margin-bottom: 0.25rem;
    color: #ccc;
    font-size: 0.9rem;
  }

  input {
    width: 100%;
    padding: 0.5rem;
    border: 1px solid #555;
    border-radius: 4px;
    background-color: #333;
    color: #fff;
    font-size: 0.9rem;
    box-sizing: border-box;
  }

  input:focus {
    outline: none;
    border-color: #0078d4;
  }

  input::placeholder {
    color: #777;
  }

  .button-group {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem;
    margin-top: 1.5rem;
  }

  button {
    padding: 0.5rem 1rem;
    border-radius: 4px;
    font-size: 0.9rem;
    cursor: pointer;
    border: none;
  }

  .cancel-btn {
    background-color: #444;
    color: #fff;
  }

  .cancel-btn:hover {
    background-color: #555;
  }

  .submit-btn {
    background-color: #0078d4;
    color: #fff;
  }

  .submit-btn:hover:not(:disabled) {
    background-color: #1084d8;
  }

  .submit-btn:disabled {
    background-color: #555;
    color: #888;
    cursor: not-allowed;
  }
</style>
