<script setup lang="ts">
const TITLE_WIDTHS = ["w-1/2", "w-2/5", "w-3/5", "w-1/3", "w-2/3"] as const;

const ROWS = Array.from({ length: 20 }, (_, index) => ({
  key: `jobs-ghost-${index}`,
  title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
}));
</script>

<template>
  <div class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
    <div v-for="row in ROWS" :key="row.key" class="flex items-center gap-4 px-4 py-3">
      <span class="size-8 shrink-0 rounded-lg border border-gray-200/70 dark:border-gray-700/70" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="row.title" />
        <span class="h-2.5 w-28 rounded-full bg-gray-500/15" />
      </span>

      <span class="hidden items-center gap-4 sm:flex">
        <span class="flex items-center gap-2">
          <span class="size-2 rounded-full bg-gray-500/25" />
          <span class="h-2.5 w-16 rounded-full bg-gray-500/15" />
        </span>

        <span class="flex items-center gap-2">
          <span class="size-2 rounded-full bg-gray-500/25" />
          <span class="h-2.5 w-12 rounded-full bg-gray-500/15" />
        </span>
      </span>

      <UIcon name="i-mdi-chevron-down" class="size-4 shrink-0 text-gray-400 dark:text-gray-600" />
    </div>
  </div>
</template>
