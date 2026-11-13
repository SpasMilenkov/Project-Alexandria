<script setup lang="ts">
const ROWS = [
  { detail: "w-40", key: "session-ghost-0", label: "w-36" },
  { detail: "w-32", key: "session-ghost-1", label: "w-44" },
  { detail: "w-36", key: "session-ghost-2", label: "w-28" },
  { detail: "w-28", key: "session-ghost-3", label: "w-40" },
  { detail: "w-40", key: "session-ghost-4", label: "w-32" },
  { detail: "w-32", key: "session-ghost-5", label: "w-36" },
] as const;
</script>

<template>
  <ul class="flex flex-col divide-y divide-gray-100/50 px-4 py-3 md:px-5 dark:divide-gray-800/50">
    <li v-for="row in ROWS" :key="row.key" class="flex items-center gap-3 py-3">
      <span class="size-5 shrink-0 rounded bg-gray-500/15" />

      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
        <span class="h-3 rounded-full bg-gray-500/25" :class="row.label" />
        <span class="h-2.5 rounded-full bg-gray-500/15" :class="row.detail" />
      </span>

      <span class="hidden h-2.5 w-20 shrink-0 rounded-full bg-gray-500/15 sm:block" />
      <span class="h-6 w-16 shrink-0 rounded-md bg-gray-500/10" />
    </li>
  </ul>
</template>
