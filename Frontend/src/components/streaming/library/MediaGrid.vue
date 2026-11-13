<template>
  <div class="flex h-full overflow-hidden">
    <section class="flex flex-col h-full overflow-hidden flex-1 min-w-0 gap-4 p-3 sm:px-3 sm:py-3">
      <div
        class="relative z-20 shrink-0 w-full rounded-2xl px-3 py-2 frosted-glass sm:px-4 glass-surface border border-black/[0.08] dark:border-white/10"
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
          <span class="text-xs text-gray-600 dark:text-gray-400 tabular-nums whitespace-nowrap">
            {{ selectedCount }} selected
          </span>
          <div class="flex-1" />
          <UButton color="neutral" variant="outline" size="xs" @click="selectAllLoaded">
            <span class="sm:hidden">Select all</span>
            <span class="max-sm:hidden">Select all loaded</span>
          </UButton>

          <UButton
            color="primary"
            size="xs"
            :disabled="selectedCount === 0"
            @click="bulkOpen = true"
          >
            <span class="sm:hidden">Edit</span>
            <span class="max-sm:hidden">Edit metadata</span>
          </UButton>
        </div>
        <div
          v-else
          class="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-2 gap-y-2 sm:flex sm:items-center sm:gap-8"
        >
          <div class="flex items-center gap-3 order-1 min-w-0">
            <h2
              class="text-xs font-semibold tracking-widest uppercase text-gray-600 dark:text-gray-400 m-0 truncate max-sm:sr-only"
            >
              Available Media
            </h2>
            <span
              v-if="data && !isInitialLoad"
              class="text-xs text-gray-600 dark:text-gray-400 tabular-nums whitespace-nowrap"
            >
              {{ formatCount(data.totalCount ?? 0, "file") }}
            </span>
          </div>

          <MediaSearchBar
            :mediaType="mediaType"
            class="order-3 col-span-2 sm:order-2 sm:flex-1 sm:min-w-0"
          />

          <div
            class="flex items-center gap-1 sm:gap-1.5 flex-wrap order-2 justify-end sm:order-3 sm:justify-start sm:shrink-0"
          >
            <button
              class="flex size-9 items-center justify-center rounded-md text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] disabled:opacity-40 disabled:cursor-not-allowed transition-all duration-150"
              aria-label="Refresh"
              :disabled="isLoading"
              @click="onRefresh"
            >
              <Icon
                icon="mdi:refresh"
                class="size-4.5 transition-transform duration-500"
                :class="{ 'animate-spin': isLoading }"
              />
            </button>

            <button
              v-if="mediaType === 'audio' && lyricsEnabled"
              class="flex size-9 items-center justify-center rounded-md transition-all duration-150"
              :class="
                lyricsOpen
                  ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
                  : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'
              "
              aria-label="Toggle lyrics"
              @click="playerStore.toggleLyrics()"
            >
              <Icon icon="mdi:script-text-outline" class="size-4.5" />
            </button>

            <div
              class="inline-flex items-center gap-0.5 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] p-0.5"
            >
              <button
                class="flex size-8 items-center justify-center rounded-md transition-all duration-150"
                :class="
                  viewMode === 'grid'
                    ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'
                "
                aria-label="Grid view"
                @click="viewMode = 'grid'"
              >
                <Icon icon="mdi:view-grid-outline" class="size-4.5" />
              </button>
              <button
                class="flex size-8 items-center justify-center rounded-md transition-all duration-150"
                :class="
                  viewMode === 'list'
                    ? 'bg-white dark:bg-white/10 text-gray-700 dark:text-white/80 shadow-sm'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'
                "
                aria-label="List view"
                @click="viewMode = 'list'"
              >
                <Icon icon="mdi:view-list-outline" class="size-4.5" />
              </button>
            </div>

            <button
              class="flex size-9 items-center justify-center rounded-md text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-all duration-150"
              aria-label="Select files"
              title="Select files"
              @click="enterSelection()"
            >
              <Icon icon="mdi:check-box-multiple-outline" class="size-4.5" />
            </button>
          </div>
        </div>
      </div>

      <!-- Loading: Grid -->
      <div
        v-if="isInitialLoad && viewMode === 'grid'"
        class="flex-1 min-h-0 flex items-center justify-center"
        role="status"
        aria-label="Loading media library"
      >
        <BlockSpinner />
      </div>

      <!-- Loading: List skeleton -->
      <div
        v-else-if="isInitialLoad"
        class="flex flex-1 min-h-0 flex-col overflow-hidden rounded-xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
      >
        <div
          class="grid h-10 shrink-0 grid-cols-[minmax(0,1fr)_36px] items-center gap-2 sm:gap-4 border-b border-gray-200/70 px-3 text-xs font-semibold text-gray-600 sm:px-4 sm:grid-cols-[minmax(0,1fr)_64px_36px] frosted-glass glass-surface-strong dark:border-gray-700/70 dark:text-gray-400"
        >
          <span>Name</span>
          <span class="hidden text-right sm:block">Duration</span>
          <span />
        </div>
        <div class="min-h-0 flex-1 overflow-hidden">
          <div
            v-for="i in 16"
            :key="i"
            :style="{ height: `${MEDIA_LIST_ROW_HEIGHT}px` }"
            class="grid grid-cols-[minmax(0,1fr)_36px] items-center gap-2 sm:gap-4 border-b border-gray-200/70 px-3 animate-pulse sm:px-4 sm:grid-cols-[minmax(0,1fr)_64px_36px] dark:border-gray-700/70"
          >
            <div class="flex min-w-0 items-center gap-2 sm:gap-3">
              <div class="hidden h-4 w-4 shrink-0 rounded bg-gray-200 sm:block dark:bg-white/10" />
              <div
                class="size-10 shrink-0 rounded-lg sm:size-11 bg-gray-200 dark:bg-white/[0.06]"
              />
              <div class="min-w-0 flex-1 space-y-1.5">
                <div class="h-3 w-2/3 rounded bg-gray-200 dark:bg-white/[0.08]" />
                <div class="h-2.5 w-1/3 rounded bg-gray-100 dark:bg-white/[0.05]" />
              </div>
            </div>
            <div
              class="ml-auto hidden h-2 w-10 rounded bg-gray-100 sm:block dark:bg-white/[0.05]"
            />
            <div class="size-9 justify-self-end rounded bg-gray-100 dark:bg-white/[0.05]" />
          </div>
        </div>
      </div>

      <!-- Error -->
      <div v-else-if="error && !allItems.length" class="flex flex-1 min-h-0 overflow-y-auto">
        <div
          class="m-auto flex flex-col items-center gap-2.5 rounded-2xl border border-gray-200/70 px-8 py-6 text-center frosted-glass glass-surface-strong dark:border-gray-700/70"
        >
          <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
          <p class="text-sm text-gray-900 dark:text-gray-100 m-0">Failed to load media library</p>
        </div>
      </div>

      <!-- Empty -->
      <EmptyStatePanel v-else-if="!allItems.length">
        <template #ghost>
          <MediaLibraryGhost :media-type="mediaType" :view-mode="viewMode" />
        </template>

        <MediaLibraryEmpty v-if="emptyMessage" :media-type="mediaType" class="m-auto" />
      </EmptyStatePanel>

      <!-- List view -->
      <div
        v-else-if="viewMode === 'list' && !isInitialLoad && allItems.length"
        class="flex flex-1 min-h-0 flex-col overflow-hidden rounded-xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
      >
        <div
          class="grid h-10 shrink-0 grid-cols-[minmax(0,1fr)_36px] items-center gap-2 sm:gap-4 border-b border-gray-200/70 px-3 text-xs font-semibold text-gray-600 sm:px-4 sm:grid-cols-[minmax(0,1fr)_64px_36px] frosted-glass glass-surface-strong dark:border-gray-700/70 dark:text-gray-400"
        >
          <span>Name</span>
          <span class="hidden text-right sm:block">Duration</span>
          <span />
        </div>
        <RecycleScroller
          class="flex-1 min-h-0"
          :items="allItems"
          :item-size="MEDIA_LIST_ROW_HEIGHT"
          key-field="fileId"
          @scroll.passive="onScroll"
        >
          <template #default="{ item }">
            <MediaCard
              :file="item"
              view-mode="list"
              :selected="selectedIds.has(item.fileId)"
              :selection-mode="selectionMode"
              :progress-percent="progressFor(item)"
              :watched="watchedFor(item)"
              @select="onFileClick"
              @info="onMediaInfo"
              @toggle="onToggleSelect"
              @longpress="onCardLongPress"
            />
          </template>
        </RecycleScroller>
      </div>

      <!-- Grid view -->
      <div
        v-else-if="viewMode === 'grid' && !isInitialLoad && allItems.length"
        class="flex-1 min-h-0"
      >
        <RecycleScroller
          class="h-full [scrollbar-gutter:stable]"
          :items="gridRows"
          :item-size="gridRowHeight"
          key-field="id"
          @scroll.passive="onScroll"
        >
          <template #before>
            <div ref="gridContainerRef" class="h-0 w-full" aria-hidden="true" />
          </template>
          <template #default="{ item: row }">
            <div class="pb-4" :style="{ height: `${gridRowHeight}px` }">
              <div
                class="grid h-full gap-x-4 items-start"
                :style="{ gridTemplateColumns: `repeat(${columnCount}, minmax(0, 1fr))` }"
              >
                <MediaCard
                  v-for="file in row.items"
                  :key="file.fileId"
                  :file="file"
                  view-mode="grid"
                  :selected="selectedIds.has(file.fileId)"
                  :selection-mode="selectionMode"
                  :progress-percent="progressFor(file)"
                  :watched="watchedFor(file)"
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

    <BulkMetadataDrawer
      :open="bulkOpen"
      :files="selectedFiles"
      @close="bulkOpen = false"
      @applied="onBulkApplied"
    />
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery, useQueryCache } from "@pinia/colada";
import { onKeyStroke } from "@vueuse/core";
import { computed, ref, watch, watchEffect } from "vue";
import { useRouter } from "vue-router";

import { type MediaFileDto } from "@/api/streaming";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import BulkMetadataDrawer from "@/components/streaming/metadata/BulkMetadataDrawer.vue";
import { isWatchedEntry, progressPercentOf, useVideoHistory } from "@/composables/useVideoHistory";
import { MEDIA_LIST_ROW_HEIGHT, getMediaGridLayout } from "@/features/media/grid-layout";
import { LIBRARY_PAGE_SIZE, libraryLabel, type SourceDescriptor } from "@/features/player/source";
import { STREAMING_QUERY_KEYS, getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { formatCount } from "@/utils/count";

import MediaCard from "./MediaCard.vue";
import MediaLibraryEmpty from "./MediaLibraryEmpty.vue";
import MediaLibraryGhost from "./MediaLibraryGhost.vue";

// Props

// Pages that explain an empty library elsewhere keep only the ghost outline here.
const { mediaType, emptyMessage = true } = defineProps<{
  mediaType: "video" | "audio";
  emptyMessage?: boolean;
}>();

// Store

const playerStore = usePlayerStore();
// Lyrics live in the shared dashboard side panel (desktop) or the mobile
// sheet, toggled through the player store. This grid owns no lyrics instance.
const lyricsOpen = computed(() => playerStore.lyricsOpen);
const lyricsEnabled = import.meta.env.VITE_LYRICS_ENABLED === "true";

const queryCache = useQueryCache();
const router = useRouter();
const libraryDescriptor = computed<SourceDescriptor>(() => ({
  isVideo: mediaType === "video",
  playlistId: null,
}));

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
  void playerStore.playTrackInContext(
    file,
    libraryDescriptor.value,
    libraryLabel(libraryDescriptor.value),
  );
};

// Watch progress for video tiles (resume bars, Watched labels).
// Disabling the query only stops fetching: audio grids still read the shared cached history,
// which includes songs, so the file itself decides whether history applies.

const { historyByFileId } = useVideoHistory(() => mediaType === "video");

const progressFor = (file: MediaFileDto): number | null => {
  if (!file.isVideo) return null;

  return progressPercentOf(historyByFileId.value.get(file.fileId), file.duration);
};

const watchedFor = (file: MediaFileDto): boolean => {
  if (!file.isVideo) return false;

  return isWatchedEntry(historyByFileId.value.get(file.fileId));
};

const onMediaInfo = (file: MediaFileDto) => {
  router.push(`/streaming/track/${file.fileId}`);
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
const containerWidth = ref(0);
const gridLayout = computed(() => getMediaGridLayout(containerWidth.value, mediaType));
const columnCount = computed(() => gridLayout.value.columns);
const gridRowHeight = computed(() => gridLayout.value.rowHeight);

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
    // Measure inside the scroller so native scrollbar width cannot skew row heights.
    containerWidth.value = entry.contentRect.width;
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
