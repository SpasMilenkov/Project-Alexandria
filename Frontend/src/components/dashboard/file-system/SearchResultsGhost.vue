<script setup lang="ts">
// Outline of a results list behind the search empty states, in the same style
// as the media library ghost: static blocks, faded out by the panel's mask.
const TITLE_WIDTHS = ["w-1/2", "w-2/3", "w-2/5", "w-3/5", "w-1/3", "w-3/4"] as const;

const sections = [
  { key: "folders", rows: 3 },
  { key: "files", rows: 6 },
].map((section) => ({
  ...section,
  rows: Array.from({ length: section.rows }, (_, index) => ({
    key: `${section.key}-${index}`,
    title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
  })),
}));
</script>

<template>
  <div class="space-y-6 py-4">
    <div v-for="section in sections" :key="section.key">
      <div class="mb-3 flex items-center gap-2 px-4">
        <span class="size-4 rounded bg-gray-500/15" />
        <span class="h-2.5 w-20 rounded-full bg-gray-500/20" />
      </div>

      <div class="space-y-1.5">
        <div
          v-for="row in section.rows"
          :key="row.key"
          class="flex h-11 items-center gap-3 border-b border-gray-200/70 px-4 dark:border-gray-700/70"
        >
          <span class="size-5 shrink-0 rounded-md bg-gray-500/15" />
          <span class="h-3 rounded-full bg-gray-500/25" :class="row.title" />
          <span class="ml-auto h-2.5 w-12 rounded-full bg-gray-500/15" />
        </div>
      </div>
    </div>
  </div>
</template>
