<template>
  <section class="px-6 pb-8">
    <!-- ── Header ── -->
    <div class="flex items-center justify-between mb-5">
      <div class="flex items-center gap-3">
        <h2
          class="text-xs font-semibold tracking-widest uppercase text-gray-400 dark:text-white/35 m-0"
        >
          Available Media
        </h2>
        <span
          v-if="data && !isLoading"
          class="text-xs text-gray-400 dark:text-white/30 tabular-nums"
        >
          {{ data.totalCount ?? 0 }} file{{ (data.totalCount ?? 0) !== 1 ? "s" : "" }}
        </span>
      </div>

      <!-- View toggle -->
      <div
        class="inline-flex items-center gap-0.5 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] p-0.5"
      >
        <button
          class="p-1.5 rounded-md transition-all duration-150"
          :class="
            viewMode === 'grid'
              ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
              : 'text-gray-400 dark:text-white/30 hover:text-gray-500 dark:hover:text-white/50'
          "
          aria-label="Grid view"
          @click="viewMode = 'grid'"
        >
          <Icon icon="mdi:view-grid-outline" class="w-4 h-4" />
        </button>
        <button
          class="p-1.5 rounded-md transition-all duration-150"
          :class="
            viewMode === 'list'
              ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
              : 'text-gray-400 dark:text-white/30 hover:text-gray-500 dark:hover:text-white/50'
          "
          aria-label="List view"
          @click="viewMode = 'list'"
        >
          <Icon icon="mdi:view-list-outline" class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- ── Loading: Grid skeleton ── -->
    <div
      v-if="isLoading && viewMode === 'grid'"
      class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3"
    >
      <div
        v-for="i in pageSize"
        :key="i"
        class="rounded-xl overflow-hidden border border-black/[0.06] dark:border-white/[0.06] animate-pulse"
      >
        <div class="aspect-video w-full bg-gray-200 dark:bg-white/4" />
        <div class="px-3 py-2.5 space-y-1.5 bg-white dark:bg-white/2">
          <div class="h-2.5 rounded bg-gray-200 dark:bg-white/6 w-3/4" />
          <div class="h-2 rounded bg-gray-100 dark:bg-white/4 w-1/2" />
        </div>
      </div>
    </div>

    <!-- ── Loading: List skeleton ── -->
    <div
      v-else-if="isLoading"
      class="flex flex-col rounded-xl border border-black/[0.06] dark:border-white/[0.06] overflow-hidden"
    >
      <div
        v-for="i in pageSize"
        :key="i"
        class="flex items-center gap-3 px-3 py-2.5 animate-pulse border-b border-black/[0.04] dark:border-white/[0.04] last:border-b-0"
      >
        <div class="w-9 h-9 rounded-lg bg-gray-200 dark:bg-white/[0.06] shrink-0" />
        <div class="flex-1 space-y-1.5">
          <div class="h-2.5 rounded bg-gray-200 dark:bg-white/[0.06] w-2/3" />
          <div class="h-2 rounded bg-gray-100 dark:bg-white/[0.04] w-1/3" />
        </div>
        <div class="h-2 rounded bg-gray-100 dark:bg-white/[0.04] w-12 shrink-0" />
      </div>
    </div>

    <!-- ── Error ── -->
    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load media library</p>
    </div>

    <!-- ── Empty ── -->
    <div
      v-else-if="!data?.items?.length"
      class="flex flex-col items-center gap-3 py-20 text-center"
    >
      <Icon icon="mdi:video-off-outline" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">No streamable media found</p>
      <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
        Files need a completed transcoding job before they appear here.
      </p>
    </div>

    <!-- ── Grid view ── -->
    <div
      v-else-if="viewMode === 'grid'"
      class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3"
    >
      <MediaCard
        @select="onFileClick"
        v-for="file in data.items"
        :key="file.fileId"
        :file="file"
        view-mode="grid"
      />
    </div>

    <!-- ── List view ── -->
    <div
      v-else
      class="flex flex-col rounded-xl border border-black/[0.06] dark:border-white/[0.06] overflow-hidden divide-y divide-black/[0.04] dark:divide-white/[0.05]"
    >
      <MediaCard
        @select="onFileClick"
        v-for="file in data.items"
        :key="file.fileId"
        :file="file"
        view-mode="list"
      />
    </div>

    <!-- ── Pagination ── -->
    <div
      v-if="data && (data.totalPages ?? 1) > 1"
      class="flex items-center justify-center gap-2 mt-6"
    >
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.04] dark:bg-white/[0.06] hover:bg-black/[0.07] dark:hover:bg-white/[0.09] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="page <= 1"
        @click="page--"
      >
        <Icon icon="mdi:chevron-left" class="w-4 h-4" />
        Prev
      </button>
      <span class="text-sm text-gray-400 dark:text-white/35 tabular-nums px-1">
        {{ page }} / {{ data.totalPages }}
      </span>
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.04] dark:bg-white/[0.06] hover:bg-black/[0.07] dark:hover:bg-white/[0.09] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="page >= (data.totalPages ?? 1)"
        @click="page++"
      >
        Next
        <Icon icon="mdi:chevron-right" class="w-4 h-4" />
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";

import MediaCard from "./MediaCard.vue";
const playerStore = usePlayerStore();
const page = ref(1);
const pageSize = ref(20);
const viewMode = ref<"grid" | "list">(
  (localStorage.getItem("media-view-mode") as "grid" | "list") || "grid",
);

watch(viewMode, (m) => localStorage.setItem("media-view-mode", m));

const { data, isLoading, error } = useQuery(() =>
  getFilesForStreaming({ page: page.value, pageSize: pageSize.value }),
);

const onFileClick = (file: MediaFileDto) => {
  const idx = data.value?.items.findIndex((f) => f.fileId === file.fileId) ?? 0;
  playerStore.setQueue(data.value?.items ?? [], idx); // initial play: set queue + active file
};

watch(
  () => data.value?.items,
  (items) => {
    if (!items?.length) return;
    if (playerStore.activePlaylistId) return;
    if (playerStore.shuffled) return;
    if (playerStore.hasActiveFile) {
      playerStore.updateQueue(items);
    } else {
      playerStore.setQueue(items);
    }
  },
  { immediate: true },
);

</script>
