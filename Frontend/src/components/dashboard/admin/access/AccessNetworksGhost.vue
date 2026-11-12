<script setup lang="ts">
const ADDRESS_WIDTHS = ["w-28", "w-24", "w-32", "w-20"] as const;

const ROWS = Array.from({ length: 10 }, (_, index) => ({
  address: ADDRESS_WIDTHS[index % ADDRESS_WIDTHS.length],
  key: `networks-ghost-${index}`,
}));
</script>

<template>
  <div class="flex flex-col p-5">
    <div v-for="row in ROWS" :key="row.key" class="flex items-center gap-3 py-2">
      <span class="size-4 shrink-0 rounded bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="row.address" />
        <span class="h-2.5 w-48 max-w-full rounded-full bg-gray-500/15" />
      </span>

      <span class="h-2.5 w-10 shrink-0 rounded-full bg-gray-500/15" />
    </div>
  </div>
</template>
