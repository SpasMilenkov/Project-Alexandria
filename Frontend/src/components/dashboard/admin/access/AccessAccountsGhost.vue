<script setup lang="ts">
const NAME_WIDTHS = ["w-24", "w-32", "w-20", "w-28"] as const;

const ROWS = Array.from({ length: 8 }, (_, index) => ({
  key: `accounts-ghost-${index}`,
  name: NAME_WIDTHS[index % NAME_WIDTHS.length],
}));
</script>

<template>
  <div class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
    <div v-for="row in ROWS" :key="row.key" class="flex items-center gap-4 px-5 py-3">
      <span class="size-7 shrink-0 rounded-full bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="row.name" />
        <span class="h-2.5 w-36 rounded-full bg-gray-500/15" />
      </span>

      <span class="hidden h-2.5 w-16 shrink-0 rounded-full bg-gray-500/15 md:block" />
      <span class="hidden h-2.5 w-8 shrink-0 rounded-full bg-gray-500/15 md:block" />
      <span class="hidden h-2.5 w-24 shrink-0 rounded-full bg-gray-500/15 lg:block" />
      <span class="hidden h-2.5 w-8 shrink-0 rounded-full bg-gray-500/15 lg:block" />
      <span class="h-5 w-14 shrink-0 rounded-full bg-gray-500/15" />
    </div>
  </div>
</template>
