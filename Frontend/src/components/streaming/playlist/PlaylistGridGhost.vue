<script setup lang="ts">
const TITLE_WIDTHS = ["w-3/4", "w-1/2", "w-2/3", "w-3/5", "w-5/6"] as const;

const CARDS = Array.from({ length: 24 }, (_, index) => ({
  key: `playlist-ghost-${index}`,
  title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
}));
</script>

<template>
  <div
    class="grid grid-cols-2 gap-4 p-4 sm:grid-cols-[repeat(auto-fill,minmax(min(100%,14rem),1fr))] sm:gap-6 sm:p-6"
  >
    <div
      v-for="card in CARDS"
      :key="card.key"
      class="overflow-hidden rounded-xl border border-gray-200/70 dark:border-gray-700/70"
    >
      <div class="aspect-square w-full bg-gray-500/15" />

      <div class="flex flex-col gap-2 p-3">
        <span class="h-3 rounded-full bg-gray-500/25" :class="card.title" />
        <span class="h-2.5 w-2/5 rounded-full bg-gray-500/15" />
      </div>
    </div>
  </div>
</template>
