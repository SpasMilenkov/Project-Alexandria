<script setup lang="ts">
const TITLE_WIDTHS = ["w-2/5", "w-1/2", "w-1/3", "w-3/5", "w-1/4"] as const;

const ROWS = Array.from({ length: 20 }, (_, index) => ({
  key: `trash-ghost-${index}`,
  title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
}));
</script>

<template>
  <div class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
    <div v-for="row in ROWS" :key="row.key" class="flex items-center gap-3 px-4 py-2.5">
      <span class="size-4 shrink-0 rounded-sm border border-gray-300/70 dark:border-gray-600/70" />
      <span class="size-5 shrink-0 rounded bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="row.title" />
        <span class="h-2.5 w-16 rounded-full bg-gray-500/15" />
      </span>

      <span class="hidden h-2.5 w-28 shrink-0 rounded-full bg-gray-500/15 sm:block" />
    </div>
  </div>
</template>
