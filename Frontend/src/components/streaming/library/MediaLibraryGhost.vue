<script setup lang="ts">
import { computed } from "vue";

import { MEDIA_CARD_METADATA_HEIGHT, MEDIA_LIST_ROW_HEIGHT } from "@/features/media/grid-layout";

const { mediaType, viewMode } = defineProps<{
  mediaType: "video" | "audio";
  viewMode: "grid" | "list";
}>();

const GRID_COLUMNS = {
  audio: "grid-cols-[repeat(auto-fill,minmax(192px,1fr))]",
  video: "grid-cols-[repeat(auto-fill,minmax(256px,1fr))]",
} as const;

const ARTWORK = {
  audio: "aspect-square",
  video: "aspect-video",
} as const;

const TITLE_WIDTHS = ["w-3/4", "w-1/2", "w-2/3", "w-3/5", "w-5/6", "w-2/5"] as const;
const SUBTITLE_WIDTHS = ["w-1/3", "w-1/2", "w-2/5", "w-1/4"] as const;

const items = computed(() => {
  const count = viewMode === "grid" ? 24 : 20;

  return Array.from({ length: count }, (_, index) => ({
    key: `library-ghost-${mediaType}-${index}`,
    title: TITLE_WIDTHS[index % TITLE_WIDTHS.length],
    subtitle: SUBTITLE_WIDTHS[index % SUBTITLE_WIDTHS.length],
  }));
});
</script>

<template>
  <div v-if="viewMode === 'grid'" class="grid gap-4 p-4" :class="GRID_COLUMNS[mediaType]">
    <div
      v-for="item in items"
      :key="item.key"
      class="overflow-hidden rounded-xl border border-gray-200/70 dark:border-gray-700/70"
    >
      <div class="w-full bg-gray-500/15" :class="ARTWORK[mediaType]" />

      <div class="flex flex-col gap-2 p-4" :style="{ height: `${MEDIA_CARD_METADATA_HEIGHT}px` }">
        <span class="h-3 rounded-full bg-gray-500/25" :class="item.title" />
        <span class="h-2.5 rounded-full bg-gray-500/15" :class="item.subtitle" />
      </div>
    </div>
  </div>

  <div v-else>
    <div
      class="grid h-10 grid-cols-[minmax(0,1fr)_36px] items-center gap-2 border-b border-gray-200/70 px-3 text-xs font-semibold text-gray-600 sm:grid-cols-[minmax(0,1fr)_64px_36px] sm:gap-4 sm:px-4 dark:border-gray-700/70 dark:text-gray-400"
    >
      <span>Name</span>
      <span class="hidden text-right sm:block">Duration</span>
      <span />
    </div>

    <div
      v-for="item in items"
      :key="item.key"
      class="grid grid-cols-[minmax(0,1fr)_36px] items-center gap-2 border-b border-gray-200/70 px-3 sm:grid-cols-[minmax(0,1fr)_64px_36px] sm:gap-4 sm:px-4 dark:border-gray-700/70"
      :style="{ height: `${MEDIA_LIST_ROW_HEIGHT}px` }"
    >
      <div class="flex min-w-0 items-center gap-2 sm:gap-3">
        <span class="hidden w-5 shrink-0 justify-center sm:flex">
          <UIcon name="i-mdi-play" class="size-4 text-gray-400 dark:text-gray-600" />
        </span>

        <span class="size-10 shrink-0 rounded-lg bg-gray-500/15 sm:size-11" />

        <div class="flex min-w-0 flex-1 flex-col gap-1.5">
          <span class="h-3 rounded-full bg-gray-500/25" :class="item.title" />
          <span class="h-2.5 rounded-full bg-gray-500/15" :class="item.subtitle" />
        </div>
      </div>

      <span class="ml-auto hidden h-2.5 w-10 rounded-full bg-gray-500/15 sm:block" />
      <span />
    </div>
  </div>
</template>
