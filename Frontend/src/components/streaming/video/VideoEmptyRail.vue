<script setup lang="ts">
const ROWS = [
  { key: "video-empty-rail-0", title: "w-3/4", subtitle: "w-1/3" },
  { key: "video-empty-rail-1", title: "w-1/2", subtitle: "w-2/5" },
  { key: "video-empty-rail-2", title: "w-2/3", subtitle: "w-1/4" },
  { key: "video-empty-rail-3", title: "w-5/6", subtitle: "w-1/2" },
  { key: "video-empty-rail-4", title: "w-3/5", subtitle: "w-1/3" },
  { key: "video-empty-rail-5", title: "w-2/3", subtitle: "w-2/5" },
] as const;
</script>

<template>
  <aside
    aria-hidden="true"
    class="pointer-events-none hidden min-h-0 min-w-0 flex-col overflow-hidden rounded-2xl border border-gray-200/70 frosted-glass glass-surface select-none xl:flex dark:border-gray-700/70"
  >
    <div
      class="flex h-[53px] shrink-0 items-center gap-2 border-b border-gray-200/70 px-4 dark:border-gray-700/70"
    >
      <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">Suggested</span>

      <span
        class="rounded-full bg-black/5 px-2 text-xs tabular-nums text-gray-600 dark:bg-white/5 dark:text-gray-400"
        >0</span
      >
    </div>

    <div
      class="flex min-h-0 flex-1 flex-col overflow-hidden p-2 [mask-image:linear-gradient(to_bottom,black_30%,transparent)]"
    >
      <div v-for="row in ROWS" :key="row.key" class="flex items-center gap-3 p-1.5">
        <span class="aspect-video w-[104px] shrink-0 rounded-lg bg-gray-500/15" />

        <span class="flex min-w-0 flex-1 flex-col gap-1.5">
          <span class="h-3 rounded-full bg-gray-500/25" :class="row.title" />
          <span class="h-2.5 rounded-full bg-gray-500/15" :class="row.subtitle" />
        </span>
      </div>
    </div>

    <div
      class="flex shrink-0 items-center gap-2 border-t border-gray-200/70 px-4 py-2.5 dark:border-gray-700/70"
    >
      <UIcon name="i-mdi-film-open-outline" class="size-3.5 shrink-0 text-primary" />
      <p class="truncate text-xs text-gray-600 dark:text-gray-400">Video library</p>
    </div>
  </aside>
</template>
