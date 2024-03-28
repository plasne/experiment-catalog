<script lang="ts">
  export let title: string;
  export let result: Result;
</script>

<div class="title">{title}</div>
<div class="set">
  <span>set:</span>
  {result?.set ?? "-"}
</div>
<div class="desc">
  <span>desc:</span>
  {result?.description ?? "-"}
</div>

<style>
  .title {
    font-size: 1.2rem;
    font-weight: bold;
    color: #ccc;
  }

  .set {
    font-size: 1rem;
  }

  .desc {
    font-size: 0.8rem;
  }
</style>
