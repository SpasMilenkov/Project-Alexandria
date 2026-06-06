<template>
  <div ref="wrapperRef" class="relative ">
    <!-- Search input -->
    <div class="relative">
      <Icon
        icon="mdi:magnify"
        class="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-white/25 pointer-events-none"
      />
      <input
        ref="inputRef"
        v-model="searchQuery"
        type="search"
        :placeholder="`Search ${mediaType === 'video' ? 'videos' : 'music'}…`"
        class="w-full pl-8 pr-7 py-1.5 text-sm rounded-lg bg-black/[0.04] dark:bg-white/[0.06] border border-transparent focus:border-black/10 dark:focus:border-white/15 focus:outline-none text-gray-700 dark:text-white/75 placeholder-gray-400 dark:placeholder-white/25 transition-colors duration-150"
        @focus="isOpen = true"
      />
      <button
        v-if="searchQuery"
        class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 dark:text-white/25 hover:text-gray-600 dark:hover:text-white/50 transition-colors duration-150"
        aria-label="Clear search"
        @click="clear"
      >
        <Icon icon="mdi:close" class="w-3.5 h-3.5" />
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
        class="absolute left-0 right-0 top-full mt-1.5 z-50 rounded-xl border border-black/[0.07] dark:border-white/[0.08] bg-white dark:bg-neutral-900 backdrop-blur-md shadow-lg shadow-black/[0.08] dark:shadow-black/30 overflow-hidden"
      >
        <!-- Loading -->
        <div v-if="isLoading" class="flex items-center justify-center py-8">
          <svg
            class="w-5 h-5 animate-spin text-gray-400 dark:text-white/30"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              class="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              stroke-width="4"
            />
            <path
              class="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 22 6.477 22 12h-4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
        </div>

        <!-- No results -->
        <div v-else-if="!results.length" class="flex flex-col items-center gap-2 py-8 text-center">
          <Icon
            icon="mdi:file-search-outline"
            class="w-8 h-8 text-gray-300 dark:text-white/[0.18]"
          />
          <p class="text-sm text-gray-500 dark:text-white/40 m-0">
            No results for "{{ debouncedQuery }}"
          </p>
        </div>

        <!-- Results -->
        <template v-else>
          <!-- Header: count + enqueue all -->
          <div
            class="flex items-center justify-between px-3 py-2 border-b border-black/[0.05] dark:border-white/[0.05]"
          >
            <span
              class="text-[11px] font-medium tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              {{ totalCount }} result{{ totalCount !== 1 ? "s" : "" }}
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
            class="max-h-72 overflow-y-auto divide-y divide-black/[0.04] dark:divide-white/[0.04]"
          >
            <MediaSearchResultRow
              v-for="file in results"
              :key="file.fileId"
              :file="file"
              :media-type="mediaType"
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
import { ref, watch } from "vue";

import { type MediaFileDto, streamingApi } from "@/api/streaming";
import { usePlayerStore } from "@/stores/stream-player";

import MediaSearchResultRow from "./MediaSearchResultRow.vue";

// Props

const { mediaType } = defineProps<{ mediaType: "video" | "audio" }>();

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
// Search results are NEVER passed to setSource — they go into the queue only.
// Navigation (next/prev) always follows the library source cursor.

const playNow = (file: MediaFileDto) => {
  playerStore.playNow([file]);
  isOpen.value = false;
  clear();
};

const enqueue = (file: MediaFileDto) => {
  playerStore.enqueue(file);
};

const enqueueAll = () => {
  results.value.forEach((f) => playerStore.enqueue(f));
  isOpen.value = false;
  clear();
};
</script>
