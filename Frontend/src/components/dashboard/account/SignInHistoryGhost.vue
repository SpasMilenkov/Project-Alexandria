<script setup lang="ts">
const SECTIONS = [
  { heading: "w-24", key: "history-ghost-logins", rows: ["w-32", "w-40", "w-28", "w-36"] },
  { heading: "w-28", key: "history-ghost-failed", rows: ["w-36", "w-28"] },
  { heading: "w-40", key: "history-ghost-addresses", rows: ["w-28", "w-32"] },
] as const;
</script>

<template>
  <div class="flex flex-col gap-6 px-4 py-3 md:px-5">
    <div v-for="section in SECTIONS" :key="section.key" class="flex flex-col gap-1">
      <span class="mb-1 h-2.5 rounded-full bg-gray-500/15" :class="section.heading" />

      <div
        v-for="(width, index) in section.rows"
        :key="`${section.key}-${index}`"
        class="flex items-center gap-3 border-b border-gray-100/50 py-2.5 dark:border-gray-800/50"
      >
        <span class="size-4 shrink-0 rounded bg-gray-500/15" />

        <span class="flex min-w-0 flex-1 flex-col gap-1.5">
          <span class="h-3 rounded-full bg-gray-500/25" :class="width" />
          <span class="h-2.5 w-24 rounded-full bg-gray-500/15" />
        </span>

        <span class="h-2.5 w-14 shrink-0 rounded-full bg-gray-500/15" />
      </div>
    </div>
  </div>
</template>
