<script lang="ts">
  export let result: Result;
  export let baseline: Result = undefined;
  export let metric: string;

  let diff: number;

  $: diff =
    result && baseline && result.metrics[metric] && baseline.metrics[metric]
      ? result.metrics[metric].value - baseline.metrics[metric].value
      : 0;
</script>

{#if result && result.metrics[metric]}
  <span>{result.metrics[metric].value.toFixed(2)}</span>
  <span>({result.metrics[metric].stdDev.toFixed(2)})</span>

  {#if diff > 0}
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
      <polygon
        points="25,10 10,40 40,40"
        style="fill:green;stroke:black;stroke-width:1"
      />
    </svg>
  {/if}
  {#if diff < 0}
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
      <polygon
        points="10,10 40,10 25,40"
        style="fill:red;stroke:black;stroke-width:1"
      />
    </svg>
  {/if}
{:else}
  <span>-</span>
{/if}

<style>
  svg {
    width: 1.2rem;
    height: 1.2rem;
  }
</style>
