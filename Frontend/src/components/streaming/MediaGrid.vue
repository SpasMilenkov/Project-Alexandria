<template>
  <div class="flex h-full overflow-hidden">
    <section class="flex flex-col h-full overflow-hidden flex-1 min-w-0">
      <div
        class="sticky top-0 z-10 px-2 pt-4 pb-3 mb-5 w-full justify-evenly frosted-glass glass-surface border border-black/[0.08] dark:border-white/10"
      >
        <div v-if="selectionMode" class="flex items-center gap-2 sm:gap-3">
          <UButton
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            size="sm"
            aria-label="Exit selection"
            @click="exitSelection"
          />
          <span class="text-xs text-gray-500 dark:text-white/30 tabular-nums whitespace-nowrap">
            {{ selectedCount }} selected
          </span>
          <div class="flex-1" />
          <UButton
            label="Select all loaded"
            color="neutral"
            variant="outline"
            size="xs"
            @click="selectAllLoaded"
          />
          <UButton
            label="Edit metadata"
            color="primary"
            size="xs"
            :disabled="selectedCount === 0"
            @click="bulkOpen = true"
          />
        </div>
        <div
          v-else
          class="grid grid-cols-2 items-center gap-x-4 gap-y-2 sm:flex sm:items-center sm:gap-8"
        >
          <div class="flex items-center gap-3 order-1">
            <h2
              class="text-xs font-semibold tracking-widest uppercase text-gray-500 dark:text-white/35 m-0"
            >
              Available Media
            </h2>
            <span
              v-if="data && !isInitialLoad"
              class="text-xs text-gray-500 dark:text-white/30 tabular-nums"
            >
              {{ data.totalCount ?? 0 }} file{{ (data.totalCount ?? 0) !== 1 ? "s" : "" }}
            </span>
          </div>

          <MediaSearchBar
            :mediaType="mediaType"
            class="order-3 col-span-2 sm:order-2 sm:flex-1 sm:min-w-0"
          />

          <div
            class="flex items-center gap-1.5 order-2 justify-end sm:order-3 sm:justify-start sm:shrink-0"
          >
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

            <button
              v-if="mediaType === 'audio'"
              class="p-1.5 rounded-md transition-all duration-150"
              :class="
                lyricsOpen
                  ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
                  : 'text-gray-400 dark:text-white/30 hover:text-gray-500 dark:hover:text-white/50'
              "
              aria-label="Toggle lyrics"
              @click="lyricsOpen = !lyricsOpen"
            >
              <Icon icon="mdi:script-text-outline" class="w-4 h-4" />
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

            <button
              class="p-1.5 rounded-md text-gray-400 dark:text-white/30 hover:text-gray-600 dark:hover:text-white/60 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-all duration-150"
              aria-label="Select files"
              title="Select files"
              @click="enterSelection()"
            >
              <Icon icon="mdi:check-box-multiple-outline" class="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      <!-- Loading: Grid skeleton -->
      <div
        v-if="isInitialLoad && viewMode === 'grid'"
        class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3"
      >
        <div
          v-for="i in LIBRARY_PAGE_SIZE"
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
          v-for="i in LIBRARY_PAGE_SIZE"
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

      <!-- Empty -->
      <div v-else-if="!allItems.length" class="flex flex-col items-center gap-3 py-20 text-center">
        <Icon icon="mdi:video-off-outline" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
        <p class="text-sm text-gray-500 dark:text-white/40 m-0">No streamable media found</p>
        <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
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
          <MediaCard
            :file="item"
            view-mode="list"
            :selected="selectedIds.has(item.fileId)"
            :selection-mode="selectionMode"
            @select="onFileClick"
            @info="onMediaInfo"
            @toggle="onToggleSelect"
            @longpress="onCardLongPress"
          />
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
                  :selected="selectedIds.has(file.fileId)"
                  :selection-mode="selectionMode"
                  @select="onFileClick"
                  @info="onMediaInfo"
                  @toggle="onToggleSelect"
                  @longpress="onCardLongPress"
                />
              </div>
            </div>
          </template>
        </RecycleScroller>
      </div>

      <!-- Fetching more indicator -->
      <div v-if="isFetchingMore" class="flex justify-center py-6">
        <BlockSpinner />
      </div>
    </section>

    <LyricsPanel v-if="mediaType === 'audio'" v-model:open="lyricsOpen" />

    <BulkMetadataDrawer
      :open="bulkOpen"
      :files="selectedFiles"
      @close="bulkOpen = false"
      @applied="onBulkApplied"
    />

    <UDrawer
      v-model:open="infoOpen"
      :title="infoTitle"
      description="Audio analysis results"
      :direction="isMobile ? 'bottom' : 'right'"
      :ui="{
        container: 'md:max-w-[34rem] lg:min-w-[44rem]',
        content: glassDrawerContent,
      }"
    >
      <template #body>
        <div class="p-1">
          <div class="flex justify-end pb-2">
            <UButton
              v-if="infoFile"
              icon="mdi:arrow-expand"
              label="Open full page"
              color="neutral"
              variant="ghost"
              size="sm"
              @click="openTrackPage"
            />
          </div>
          <AudioAnalysisFilePanel v-if="infoFile" :file-id="infoFile.fileId" :enabled="infoOpen" />
        </div>
      </template>
    </UDrawer>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery, useQueryCache } from "@pinia/colada";
import { breakpointsTailwind, onKeyStroke, useBreakpoints } from "@vueuse/core";
import { computed, ref, watch, watchEffect } from "vue";
import { useRouter } from "vue-router";

import { type MediaFileDto, streamingApi } from "@/api/streaming";
import { LIBRARY_PAGE_SIZE } from "@/composables/useStreamingMediaContext";
import { STREAMING_QUERY_KEYS, getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { glassDrawerContent } from "@/utils/modalUi";

import BlockSpinner from "../common/BlockSpinner.vue";
import AudioAnalysisFilePanel from "../dashboard/integrations/audio-analysis/AudioAnalysisFilePanel.vue";
import BulkMetadataDrawer from "./BulkMetadataDrawer.vue";
import LyricsPanel from "./LyricsPanel.vue";
import MediaCard from "./MediaCard.vue";

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

// Props

const { mediaType } = defineProps<{ mediaType: "video" | "audio" }>();
const lyricsOpen = ref(false);

// Store

const playerStore = usePlayerStore();
const queryCache = useQueryCache();
const router = useRouter();
const mySourceId = computed(() => `library-${mediaType}`);

// View mode (persisted to localStorage)

const storageKey = `media-view-mode-${mediaType}`;
const defaultMode = mediaType === "audio" ? "list" : "grid";
const viewMode = ref<"grid" | "list">(
  (localStorage.getItem(storageKey) as "grid" | "list") || defaultMode,
);
watch(viewMode, (m) => localStorage.setItem(storageKey, m));

// Accumulates raw library pages for the virtual scroller.
// Completely independent from the player store's sliding window.

const page = ref(1);
const allItems = ref<MediaFileDto[]>([]);

watch(
  () => mediaType,
  () => {
    allItems.value = [];
    page.value = 1;
    exitSelection();
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
    pageSize: LIBRARY_PAGE_SIZE,
    isVideo: mediaType === "video",
    query: null,
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
  page.value = 1;
  exitSelection();
  // refresh() is a no-op on fresh entries, so mark page 1 stale first.
  // Otherwise the clear above empties the grid with nothing repopulating it
  // whenever the data is still within staleTime.
  queryCache.invalidateQueries({
    key: STREAMING_QUERY_KEYS.filesForStreaming({
      isVideo: mediaType === "video",
      page: 1,
      pageSize: LIBRARY_PAGE_SIZE,
      query: null,
    }),
  });
  refetchQuery();
};

const onFileClick = (file: MediaFileDto) => {
  const globalIndex = allItems.value.findIndex((f) => f.fileId === file.fileId);
  if (globalIndex === -1) return;

  const sourcePage = Math.floor(globalIndex / LIBRARY_PAGE_SIZE) + 1;
  const indexInPage = globalIndex % LIBRARY_PAGE_SIZE;
  const pageStart = (sourcePage - 1) * LIBRARY_PAGE_SIZE;
  const pageItems = allItems.value.slice(pageStart, pageStart + LIBRARY_PAGE_SIZE);

  playerStore.setSource(
    pageItems,
    mySourceId.value,
    sourcePage,
    indexInPage,
    data.value?.totalPages ?? 1,
    (p) =>
      streamingApi.getFilesForStreaming({
        page: p,
        pageSize: LIBRARY_PAGE_SIZE,
        isVideo: mediaType === "video",
        query: null,
      }),
  );
};

// Audio analysis drawer

const infoOpen = ref(false);
const infoFile = ref<MediaFileDto | null>(null);

const infoTitle = computed(() => infoFile.value?.title ?? infoFile.value?.fileName ?? "");

const onMediaInfo = (file: MediaFileDto) => {
  infoFile.value = file;
  infoOpen.value = true;
};

const openTrackPage = () => {
  if (!infoFile.value) return;
  const fileId = infoFile.value.fileId;
  infoOpen.value = false;
  infoFile.value = null;
  router.push(`/streaming/track/${fileId}`);
};

// Selection mode: id-based state owned here so recycled cards cannot drift.
// Cards render purely from props; clicks flip meaning based on the mode.

const selectionMode = ref(false);
const selectedIds = ref(new Set<string>());
const lastToggleIndex = ref<number | null>(null);
const bulkOpen = ref(false);

const selectedCount = computed(() => selectedIds.value.size);

const selectedFiles = computed(() => {
  const ids = selectedIds.value;
  return allItems.value.filter((file) => ids.has(file.fileId));
});

const enterSelection = (file?: MediaFileDto) => {
  selectionMode.value = true;
  if (file) {
    selectedIds.value = new Set(selectedIds.value).add(file.fileId);
    lastToggleIndex.value = allItems.value.findIndex((f) => f.fileId === file.fileId);
  }
};

const exitSelection = () => {
  selectionMode.value = false;
  selectedIds.value = new Set();
  lastToggleIndex.value = null;
};

const onToggleSelect = (file: MediaFileDto, event: MouseEvent) => {
  const currentIndex = allItems.value.findIndex((f) => f.fileId === file.fileId);
  if (
    selectionMode.value &&
    event.shiftKey &&
    lastToggleIndex.value !== null &&
    currentIndex !== -1
  ) {
    const [start, end] = [
      Math.min(lastToggleIndex.value, currentIndex),
      Math.max(lastToggleIndex.value, currentIndex),
    ];
    const next = new Set(selectedIds.value);
    for (let i = start; i <= end; i++) next.add(allItems.value[i].fileId);
    selectedIds.value = next;
    lastToggleIndex.value = currentIndex;
    return;
  }
  const next = new Set(selectedIds.value);
  if (next.has(file.fileId)) next.delete(file.fileId);
  else next.add(file.fileId);
  if (!selectionMode.value) selectionMode.value = true;
  if (next.size === 0) {
    exitSelection();
    return;
  }
  selectedIds.value = next;
  lastToggleIndex.value = currentIndex === -1 ? null : currentIndex;
};

const onCardLongPress = (file: MediaFileDto) => {
  if (!selectionMode.value) enterSelection(file);
  else if (!selectedIds.value.has(file.fileId)) onToggleSelect(file, new MouseEvent("click"));
};

const selectAllLoaded = () => {
  selectionMode.value = true;
  selectedIds.value = new Set(allItems.value.map((file) => file.fileId));
  lastToggleIndex.value = null;
};

const onBulkApplied = () => {
  bulkOpen.value = false;
  exitSelection();
};

onKeyStroke("Escape", () => {
  if (bulkOpen.value) return;
  if (selectionMode.value) exitSelection();
});

// Grid layout

const gridContainerRef = ref<HTMLElement | null>(null);
const columnCount = ref(5);
const containerWidth = ref(0);

const gridRowHeight = computed(() => {
  if (!containerWidth.value) return 240;
  const gap = 12;
  const colWidth = (containerWidth.value - (columnCount.value - 1) * gap) / columnCount.value;
  const thumbHeight = Math.round(colWidth * (mediaType === "video" ? 9.5 / 16 : 1));
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
