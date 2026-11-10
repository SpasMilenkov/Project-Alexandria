<template>
  <div ref="wrapperRef" class="relative">
    <!-- Search input -->
    <div class="relative">
      <Icon
        icon="mdi:magnify"
        class="absolute z-10 top-1/2 -translate-y-1/2 pointer-events-none"
        :class="sizeClasses.icon"
      />
      <input
        ref="inputRef"
        v-model="searchQuery"
        type="search"
        :placeholder="`${searchLabel}…`"
        :aria-label="searchLabel"
        class="w-full focus:outline-none transition-colors duration-150"
        :class="sizeClasses.input"
        @focus="isOpen = true"
      />
      <button
        v-if="searchQuery"
        class="absolute z-10 top-1/2 -translate-y-1/2 flex items-center justify-center text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 transition-colors duration-150"
        :class="sizeClasses.clear"
        aria-label="Clear search"
        @click="clear"
      >
        <Icon icon="mdi:close" :class="sizeClasses.clearIcon" />
      </button>
    </div>

    <!-- Dropdown -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="opacity-0 translate-y-1 scale-[0.98]"
      enter-to-class="opacity-100 translate-y-0 scale-100"
      leave-active-class="transition-all duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0 scale-100"
      leave-to-class="opacity-0 translate-y-1 scale-[0.98]"
    >
      <div
        v-if="isOpen && debouncedQuery"
        class="absolute left-0 right-0 top-full z-50 frosted-glass glass-surface-strong overflow-hidden"
        :class="sizeClasses.dropdown"
      >
        <!-- Loading -->
        <div v-if="isLoading" class="flex items-center justify-center py-8">
          <BlocksSpinner :size="11" aria-label="Refreshing" class="opacity-50" />
        </div>

        <!-- No results -->
        <div
          v-else-if="!results.length"
          class="flex flex-col items-center justify-center gap-2 px-6 py-8 text-center"
        >
          <Icon icon="mdi:file-search-outline" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
          <p class="text-sm text-gray-600 dark:text-gray-400 m-0">
            No results for "{{ debouncedQuery }}"
          </p>
        </div>

        <!-- Results -->
        <template v-else>
          <!-- Header: count + enqueue all -->
          <div
            class="flex items-center justify-between py-2 border-b border-black/[0.05] dark:border-white/[0.05]"
            :class="sizeClasses.dropdownHeader"
          >
            <span
              class="text-[11px] font-medium tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              {{ formatCount(totalCount, "result") }}
            </span>
            <UButton
              size="xs"
              variant="ghost"
              color="neutral"
              icon="i-heroicons-queue-list"
              label="Enqueue all"
              @click="enqueueAll"
            />
          </div>

          <!-- Result rows -->
          <ul
            class="overflow-y-auto divide-y divide-black/[0.04] dark:divide-white/[0.04]"
            :class="sizeClasses.list"
          >
            <MediaSearchResultRow
              v-for="file in results"
              :key="file.fileId"
              :file="file"
              :media-type="mediaType"
              :size="size"
              @play-now="playNow"
              @enqueue="enqueue"
            />
          </ul>
        </template>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { onClickOutside } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import { type MediaFileDto, streamingApi } from "@/api/streaming";
import BlocksSpinner from "@/components/common/BlockSpinner.vue";
import { usePlayerStore } from "@/stores/stream-player";
import { formatCount } from "@/utils/count";

import MediaSearchResultRow from "./MediaSearchResultRow.vue";

// Props

const { mediaType, size = "md" } = defineProps<{
  mediaType: "video" | "audio";
  size?: "md" | "lg";
}>();

const searchLabel = computed(() => `Search ${mediaType === "video" ? "videos" : "music"}`);

const SIZE_CLASSES = {
  md: {
    icon: "left-2.5 w-4 h-4 text-gray-400 dark:text-white/25",
    input:
      "pl-8 pr-7 py-2 text-sm rounded-lg bg-black/[0.04] dark:bg-white/[0.06] border border-transparent focus:border-black/10 dark:focus:border-white/15 text-gray-700 dark:text-white/75 placeholder-gray-400 dark:placeholder-white/25",
    clear: "right-2",
    clearIcon: "w-3.5 h-3.5",
    dropdown:
      "mt-1.5 rounded-xl border border-black/[0.07] dark:border-white/[0.08] shadow-lg shadow-black/[0.08] dark:shadow-black/30",
    dropdownHeader: "px-3",
    list: "max-h-72",
  },
  lg: {
    icon: "left-4 w-5 h-5 text-gray-600 dark:text-gray-400",
    input:
      "h-11 pl-11 pr-11 text-base rounded-full frosted-glass glass-surface-strong  border border-gray-200/70 dark:border-gray-700/70 focus:border-primary text-gray-900 dark:text-gray-100 placeholder-gray-500 dark:placeholder-gray-400",
    clear: "right-1.5 size-8 rounded-full",
    clearIcon: "w-4 h-4",
    dropdown: "mt-2 rounded-3xl border border-gray-200/70 dark:border-gray-700/70 shadow-xl",
    dropdownHeader: "px-6",
    list: "max-h-[min(28rem,60vh)]",
  },
} as const;

const sizeClasses = computed(() => SIZE_CLASSES[size]);

// Store

const playerStore = usePlayerStore();

// Search state

const searchQuery = ref("");
const debouncedQuery = ref("");
const isOpen = ref(false);
const isLoading = ref(false);
const results = ref<MediaFileDto[]>([]);
const totalCount = ref(0);

let debounceTimer: ReturnType<typeof setTimeout> | null = null;

watch(searchQuery, (val) => {
  if (debounceTimer) clearTimeout(debounceTimer);
  const trimmed = val.trim();
  if (!trimmed) {
    debouncedQuery.value = "";
    results.value = [];
    totalCount.value = 0;
    return;
  }
  debounceTimer = setTimeout(() => {
    debouncedQuery.value = trimmed;
  }, 300);
});

watch(debouncedQuery, async (query) => {
  if (!query) return;
  isLoading.value = true;
  try {
    const data = await streamingApi.getFilesForStreaming({
      page: 1,
      pageSize: 20,
      isVideo: mediaType === "video",
      query,
    });
    results.value = data.items;
    totalCount.value = data.totalCount ?? data.items.length;
  } catch {
    results.value = [];
    totalCount.value = 0;
  } finally {
    isLoading.value = false;
  }
});

const clear = () => {
  if (debounceTimer) clearTimeout(debounceTimer);
  searchQuery.value = "";
  debouncedQuery.value = "";
  results.value = [];
  totalCount.value = 0;
};
const wrapperRef = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLInputElement | null>(null);

onClickOutside(wrapperRef, () => {
  isOpen.value = false;
});

// Player actions
//
// Search results are always interrupts: playNow leaves the context cursor
// untouched, and queue adds never disturb the active context.

const playNow = (file: MediaFileDto) => {
  playerStore.playNow([file]);
  isOpen.value = false;
  clear();
};

const enqueue = (file: MediaFileDto) => {
  void playerStore.addToQueue([file]);
};

const enqueueAll = () => {
  void playerStore.addToQueue([...results.value]);
  isOpen.value = false;
  clear();
};
</script>
