<script setup lang="ts">
const REASONS = [
  { bar: "w-4/5", key: "reason-ghost-0", label: "w-32" },
  { bar: "w-1/2", key: "reason-ghost-1", label: "w-40" },
  { bar: "w-1/5", key: "reason-ghost-2", label: "w-28" },
] as const;

const SOURCES = Array.from({ length: 6 }, (_, index) => `source-ghost-${index}`);
</script>

<template>
  <div class="flex flex-col gap-5 p-5">
    <div class="flex flex-col gap-3">
      <span class="h-2.5 w-24 rounded-full bg-gray-500/15" />

      <div v-for="reason in REASONS" :key="reason.key" class="flex flex-col gap-1.5">
        <span class="flex items-center justify-between gap-3">
          <span class="h-3 rounded-full bg-gray-500/25" :class="reason.label" />
          <span class="h-3 w-5 rounded-full bg-gray-500/25" />
        </span>
        <span class="h-1.5 overflow-hidden rounded-full bg-gray-500/10">
          <span class="block h-full rounded-full bg-gray-500/25" :class="reason.bar" />
        </span>
      </div>
    </div>

    <div class="flex flex-col gap-2">
      <span class="h-2.5 w-32 rounded-full bg-gray-500/15" />

      <div v-for="key in SOURCES" :key="key" class="flex items-center gap-3 py-1.5">
        <span class="size-4 shrink-0 rounded bg-gray-500/15" />
        <span class="flex min-w-0 flex-1 flex-col gap-1.5">
          <span class="h-3 w-28 rounded-full bg-gray-500/25" />
          <span class="h-2.5 w-36 rounded-full bg-gray-500/15" />
        </span>
        <span class="h-2.5 w-10 shrink-0 rounded-full bg-gray-500/15" />
      </div>
    </div>
  </div>
</template>
