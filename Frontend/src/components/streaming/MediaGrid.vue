<template>
  <section class="px-2 flex flex-col h-full overflow-hidden">
    <div
      class="sticky top-0 z-10 -mx-6 px-6 pt-4 pb-3 mb-5 bg-white/90 dark:bg-neutral-950/90 backdrop-blur-md border-b border-black/[0.04] dark:border-white/[0.04]"
    >
      <div class="flex items-center justify-between mb-2.5">
        <div class="flex items-center gap-3">
          <h2
            class="text-xs font-semibold tracking-widest uppercase text-gray-400 dark:text-white/35 m-0"
          >
            Available Media
          </h2>
          <span
            v-if="data && !isInitialLoad"
            class="text-xs text-gray-400 dark:text-white/30 tabular-nums"
          >
            {{ data.totalCount ?? 0 }} file{{ (data.totalCount ?? 0) !== 1 ? "s" : "" }}
          </span>
        </div>

        <div class="flex items-center gap-1.5">
          <button
            class="p-1.5 rounded-md text-gray-400 dark:text-white/30 hover:text-gray-600 dark:hover:text-white/60 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] disabled:opacity-40 disabled:cursor-not-allowed transition-all duration-150"
            aria-label="Refresh"
            :disabled="isLoading"
            @click="onRefresh"
          >
            <Icon
              icon="mdi:refresh"
              class="w-4 h-4 transition-transform duration-500"
              :class="{ 'animate-spin': isLoading }"
            />
          </button>

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
      </div>

      <div class="relative">
        <Icon
          icon="mdi:magnify"
          class="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-white/25 pointer-events-none"
        />
        <input
          v-model="searchQuery"
          type="search"
          placeholder="Search…"
          class="w-full pl-8 pr-7 py-1.5 text-sm rounded-lg bg-black/[0.04] dark:bg-white/[0.06] border border-transparent focus:border-black/10 dark:focus:border-white/15 focus:outline-none text-gray-700 dark:text-white/75 placeholder-gray-400 dark:placeholder-white/25 transition-colors duration-150"
        />
        <button
          v-if="searchQuery"
          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 dark:text-white/25 hover:text-gray-600 dark:hover:text-white/50 transition-colors duration-150"
          aria-label="Clear search"
          @click="clearSearch"
        >
          <Icon icon="mdi:close" class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>

    <!-- Loading: Grid skeleton -->
    <div
      v-if="isInitialLoad && viewMode === 'grid'"
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

    <!-- Loading: List skeleton -->
    <div
      v-else-if="isInitialLoad"
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

    <!-- Error -->
    <div
      v-else-if="error && !allItems.length"
      class="flex flex-col items-center gap-2.5 py-20 text-center"
    >
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load media library</p>
    </div>

    <!-- Empty / no results -->
    <div v-else-if="!allItems.length" class="flex flex-col items-center gap-3 py-20 text-center">
      <Icon
        :icon="debouncedQuery ? 'mdi:file-search-outline' : 'mdi:video-off-outline'"
        class="w-10 h-10 text-gray-300 dark:text-white/[0.18]"
      />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">
        {{ debouncedQuery ? `No results for "${debouncedQuery}"` : "No streamable media found" }}
      </p>
      <p
        v-if="!debouncedQuery"
        class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0"
      >
        Files need a completed transcoding job before they appear here.
      </p>
    </div>

    <!-- List view -->
    <RecycleScroller
      v-else-if="viewMode === 'list' && !isInitialLoad && allItems.length"
      class="flex-1 min-h-0 rounded-xl border border-black/[0.06] dark:border-white/[0.06] divide-y divide-black/[0.04] dark:divide-white/[0.05]"
      :items="allItems"
      :item-size="60"
      key-field="fileId"
      @scroll.passive="onScroll"
    >
      <template #default="{ item }">
        <MediaCard :file="item" view-mode="list" @select="onFileClick" />
      </template>
    </RecycleScroller>

    <!-- Grid view -->
    <div
      v-else-if="viewMode === 'grid' && !isInitialLoad && allItems.length"
      ref="gridContainerRef"
      class="flex-1 min-h-0"
    >
      <RecycleScroller
        class="h-full"
        :items="gridRows"
        :item-size="gridRowHeight"
        key-field="id"
        @scroll.passive="onScroll"
      >
        <template #default="{ item: row }">
          <div class="pb-3" :style="{ height: `${gridRowHeight}px` }">
            <div
              class="grid h-full gap-x-3"
              :style="{ gridTemplateColumns: `repeat(${columnCount}, minmax(0, 1fr))` }"
            >
              <MediaCard
                v-for="file in row.items"
                :key="file.fileId"
                :file="file"
                view-mode="grid"
                @select="onFileClick"
              />
            </div>
          </div>
        </template>
      </RecycleScroller>
    </div>

    <!-- Fetching more indicator -->
    <div v-if="isFetchingMore" class="flex justify-center py-6">
      <svg
        class="w-5 h-5 animate-spin text-gray-400 dark:text-white/30"
        xmlns="http://www.w3.org/2000/svg"
        fill="none"
        viewBox="0 0 24 24"
      >
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
        <path
          class="opacity-75"
          fill="currentColor"
          d="M4 12a8 8 0 018-8V0C5.373 0 22 6.477 22 12h-4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
        />
      </svg>
    </div>
  </section>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, onMounted, onUnmounted, ref, watch, watchEffect } from "vue";

import { type MediaFileDto, streamingApi } from "@/api/streaming";
import { getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";

import MediaCard from "./MediaCard.vue";

const { mediaType } = defineProps<{ mediaType: "video" | "audio" }>();

const playerStore = usePlayerStore();
const mySourceId = computed(() => `library-${mediaType}`);

const storageKey = `media-view-mode-${mediaType}`;
const defaultMode = mediaType === "audio" ? "list" : "grid";
const viewMode = ref<"grid" | "list">(
  (localStorage.getItem(storageKey) as "grid" | "list") || defaultMode,
);
watch(viewMode, (m) => localStorage.setItem(storageKey, m));

const searchQuery = ref("");
const debouncedQuery = ref("");
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

watch(searchQuery, (val) => {
  if (debounceTimer) clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    debouncedQuery.value = val.trim();
  }, 300);
});

const clearSearch = () => {
  if (debounceTimer) clearTimeout(debounceTimer);
  searchQuery.value = "";
  debouncedQuery.value = "";
};

const page = ref(1);
const pageSize = ref(20);
const allItems = ref<MediaFileDto[]>([]);

watch(debouncedQuery, () => {
  allItems.value = [];
  page.value = 1;
});

watch(
  () => mediaType,
  () => {
    allItems.value = [];
    page.value = 1;
  },
);

const {
  data,
  isLoading,
  error,
  refresh: refetchQuery,
} = useQuery(() =>
  getFilesForStreaming({
    page: page.value,
    pageSize: pageSize.value,
    isVideo: mediaType === "video",
    query: debouncedQuery.value || null,
  }),
);

watch(
  data,
  (newData) => {
    if (!newData?.items) return;
    if (page.value === 1) {
      allItems.value = newData.items;
    } else {
      const existingIds = new Set(allItems.value.map((f) => f.fileId));
      const fresh = newData.items.filter((f) => !existingIds.has(f.fileId));
      allItems.value = [...allItems.value, ...fresh];
    }
  },
  { immediate: true },
);

const isInitialLoad = computed(() => isLoading.value && allItems.value.length === 0);
const isFetchingMore = computed(() => isLoading.value && allItems.value.length > 0);
const hasMore = computed(() => page.value < (data.value?.totalPages ?? 1));

const loadMore = () => {
  if (isLoading.value || !hasMore.value) return;
  page.value++;
};

const onScroll = (event: Event) => {
  const el = event.target as HTMLElement;
  if (el.scrollTop + el.clientHeight > el.scrollHeight - 400 && hasMore.value && !isLoading.value) {
    loadMore();
  }
};

const onRefresh = () => {
  allItems.value = [];
  const wasAlreadyOnFirstPage = page.value === 1;
  page.value = 1;
  if (wasAlreadyOnFirstPage) refetchQuery();
};

const onFileClick = async (file: MediaFileDto) => {
  const baseParams = {
    isVideo: mediaType === "video",
    pageSize: 50,
    query: debouncedQuery.value || null,
  };

  const firstPage = await streamingApi.getFilesForStreaming({ ...baseParams, page: 1 });

  const idx = Math.max(
    0,
    firstPage.items.findIndex((f) => f.fileId === file.fileId),
  );

  playerStore.setSource(firstPage.items, mySourceId.value, idx, {
    loadedPage: 1,
    totalPages: firstPage.totalPages,
    fetchPage: (page) => streamingApi.getFilesForStreaming({ ...baseParams, page }),
  });
};

const gridContainerRef = ref<HTMLElement | null>(null);
const columnCount = ref(5);
const containerWidth = ref(0);

const gridRowHeight = computed(() => {
  if (!containerWidth.value) return 240;
  const gap = 12;
  const colWidth = (containerWidth.value - (columnCount.value - 1) * gap) / columnCount.value;
  const thumbHeight = Math.round(colWidth * (mediaType === "video" ? 9.5  / 16 : 1));
  return thumbHeight;
});

const gridRows = computed(() => {
  const cols = columnCount.value;
  return Array.from({ length: Math.ceil(allItems.value.length / cols) }, (_, i) => ({
    id: `row-${i}-${allItems.value[i * cols]?.fileId ?? i}`,
    items: allItems.value.slice(i * cols, (i + 1) * cols),
  }));
});

watchEffect((onCleanup) => {
  const el = gridContainerRef.value;
  if (!el) return;
  const ro = new ResizeObserver(([entry]) => {
    const w = entry.contentRect.width;
    containerWidth.value = w;
    if (w < 640) columnCount.value = 1;
    else if (w < 768) columnCount.value = 2;
    else if (w < 1024) columnCount.value = 3;
    else if (w < 1280) columnCount.value = 4;
    else columnCount.value = 5;
  });
  ro.observe(el);
  onCleanup(() => ro.disconnect());
});
</script>

<style>
.vue-recycle-scroller {
  border: none;
}
</style>
