<script setup lang="ts">
const { viewMode } = defineProps<{ viewMode: "grid" | "list" }>();

const TITLE_WIDTHS = ["w-3/5", "w-2/5", "w-1/2", "w-2/3", "w-1/3"] as const;

const ITEMS = Array.from({ length: 30 }, (_, index) => ({
  key: `tag-ghost-${index}`,
  title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
}));
</script>

<template>
  <div
    v-if="viewMode === 'grid'"
    class="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-2 p-3"
  >
    <div
      v-for="item in ITEMS"
      :key="item.key"
      class="flex items-center gap-3 rounded-lg border border-gray-200/70 p-3 dark:border-gray-700/70"
    >
      <span class="size-10 shrink-0 rounded-lg bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="item.title" />
        <span class="h-2.5 w-1/3 rounded-full bg-gray-500/15" />
      </span>
    </div>
  </div>

  <div v-else class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
    <div v-for="item in ITEMS" :key="item.key" class="flex items-center gap-3 px-4 py-2.5">
      <span class="size-8 shrink-0 rounded-lg bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="item.title" />
        <span class="h-2.5 w-24 rounded-full bg-gray-500/15" />
      </span>

      <span class="hidden h-2.5 w-20 shrink-0 rounded-full bg-gray-500/15 sm:block" />
    </div>
  </div>
</template>
