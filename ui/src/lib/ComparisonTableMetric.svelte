<script lang="ts">
  export let result: Result;
  export let baseline: Result = undefined;
  export let metric: string;
  export let showStdDev: boolean = true;
  export let showCount: boolean = true;

  let isCount: boolean;
  let isCost: boolean;
  let isAvg: boolean;
  let lowerIsBetter: boolean;

  const hasTag = (tag: string): boolean =>
    result &&
    result.metrics &&
    result.metrics[metric] &&
    result.metrics[metric].tags &&
    result.metrics[metric].tags.includes(tag);

  $: {
    isCount = hasTag("count");
    isCost = hasTag("cost");
    isAvg = !(isCount || isCost);
    lowerIsBetter = hasTag("lower-is-better");
  }

  let diff: number;
  let difp: number;
  let opacity: number;

  $: {
    diff =
      result &&
      baseline &&
      result.metrics &&
      baseline.metrics &&
      result.metrics[metric] &&
      baseline.metrics[metric]
        ? result.metrics[metric].value - baseline.metrics[metric].value
        : 0;
    difp =
      result &&
      baseline &&
      result.metrics &&
      baseline.metrics &&
      result.metrics[metric] &&
      baseline.metrics[metric] &&
      result.metrics[metric].normalized !== undefined &&
      baseline.metrics[metric].normalized !== undefined
        ? (result.metrics[metric].normalized -
            baseline.metrics[metric].normalized) /
          baseline.metrics[metric].normalized
        : undefined;
    opacity = 30 + Math.abs(difp) * (80 - 30) * 4;
  }
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
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 40 40"
        style="opacity: {opacity}%"
      >
        <polygon
          points="25,10 10,40 40,40"
          style="fill:{lowerIsBetter
            ? 'red'
            : 'green'};stroke:black;stroke-width:1"
        />
      </svg>
    {/if}
    {#if isAvg && diff < 0}
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 40 40"
        style="opacity: {opacity}%"
      >
        <polygon
          points="10,10 40,10 25,40"
          style="fill:{lowerIsBetter
            ? 'green'
            : 'red'};stroke:black;stroke-width:1"
        />
      </svg>
    {/if}

    {#if difp != undefined && !Number.isNaN(difp) && Number.isFinite(difp) && !lowerIsBetter}
      <span class:difp-red={difp < 0} class:difp-green={difp > 0}
        >{difp > 0 ? "+" : ""}{(difp * 100).toFixed(0)}%</span
      >
    {/if}
    {#if difp != undefined && !Number.isNaN(difp) && Number.isFinite(difp) && lowerIsBetter}
      <span class:difp-red={difp > 0} class:difp-green={difp < 0}
        >{difp > 0 ? "+" : ""}{(difp * 100).toFixed(0)}%</span
      >
    {/if}
    {#if difp != undefined && !Number.isNaN(difp) && !Number.isFinite(difp)}
      <span class:difp-green={!lowerIsBetter} class:difp-red={lowerIsBetter}
        >&infin;%</span
      >
    {/if}
    {#if difp != undefined && Number.isNaN(difp) && diff === 0}
      <span>0%</span>
    {/if}
    {#if difp != undefined && Number.isNaN(difp) && diff < 0}
      <span class:difp-green={lowerIsBetter} class:difp-red={!lowerIsBetter}
        >&infin;%</span
      >
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

  .difp-red {
    color: #f66;
  }

  .difp-green {
    color: #6a6;
  }
</style>
