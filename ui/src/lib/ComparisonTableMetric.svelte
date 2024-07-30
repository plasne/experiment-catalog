<script lang="ts">
  export let result: Result;
  export let baseline: Result = undefined;
  export let metric: string;
  export let showStdDev: boolean = true;
  export let showCount: boolean = true;

  let isCount: boolean;
  let isCost: boolean;
  let isAvg: boolean;
  
  $: {
    isCount = metric.indexOf("count") > -1;
    isCost = metric.indexOf("cost") > -1;
    isAvg = !(isCount || isCost);
  }

  let diff: number;

  $: diff =
    result &&
    baseline &&
    result.metrics &&
    baseline.metrics &&
    result.metrics[metric] &&
    baseline.metrics[metric]
      ? result.metrics[metric].value - baseline.metrics[metric].value
      : 0;
</script>

<nobr>
  {#if result && result.metrics && result.metrics[metric]}
    {#if isCount}
      <span>{result.metrics[metric].value.toLocaleString()}</span>
    {:else if isCost}
      <span>${result.metrics[metric].value.toFixed(2).toLocaleString()}</span>
    {:else if result.metrics[metric].value == undefined}
      <span>-</span>
    {:else}
      <span
        >{result.metrics[metric].value.toFixed(3) === "0.000" &&
        result.metrics[metric].value > 0
          ? ">0.00"
          : result.metrics[metric].value.toFixed(3)}</span
      >
    {/if}
    {#if showStdDev && isAvg && result.metrics[metric].std_dev !== undefined}
      <span>({result.metrics[metric].std_dev.toFixed(3)})</span>
    {/if}

    {#if isAvg && diff === 0 && result.metrics[metric].value !== undefined}
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
        <polygon
          points="10,15 35,15 35,35 10,35"
          style="fill:gray;stroke:black;stroke-width:1"
        />
      </svg>
    {/if}
    {#if isAvg && diff > 0}
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
        <polygon
          points="25,10 10,40 40,40"
          style="fill:green;stroke:black;stroke-width:1"
        />
      </svg>
    {/if}
    {#if isAvg && diff < 0}
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
        <polygon
          points="10,10 40,10 25,40"
          style="fill:red;stroke:black;stroke-width:1"
        />
      </svg>
    {/if}
    {#if showCount && isAvg}
      <span>x{result.metrics[metric].count}</span>
    {/if}
  {:else}
    <span>-</span>
  {/if}
</nobr>

<style>
  svg {
    width: 1.2rem;
    height: 1.2rem;
  }
</style>
