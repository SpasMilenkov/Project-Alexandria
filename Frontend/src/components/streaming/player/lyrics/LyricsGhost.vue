<script setup lang="ts">
const LINE_WIDTHS = [
  "w-3/4",
  "w-1/2",
  "w-2/3",
  "w-5/6",
  "w-3/5",
  "w-2/5",
  "w-4/5",
  "w-1/2",
] as const;

// Every fourth line closes a verse, so the ghost reads as lyrics rather than a list.
const LINES = Array.from({ length: 24 }, (_, index) => ({
  key: `lyrics-ghost-${index}`,
  verseEnd: index % 4 === 3,
  width: LINE_WIDTHS[index % LINE_WIDTHS.length],
}));
</script>

<template>
  <div class="flex flex-col gap-3 px-5 py-6">
    <span
      v-for="line in LINES"
      :key="line.key"
      class="h-3 shrink-0 rounded-full bg-gray-500/15"
      :class="[line.width, { 'mb-4': line.verseEnd }]"
    />
  </div>
</template>
