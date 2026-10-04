<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";
import type { UpNextItem } from "@/stores/player/types";

import { isWatchedEntry, progressPercentOf, useVideoHistory } from "@/composables/useVideoHistory";
import { usePlayerStore } from "@/stores/stream-player";

import VideoUpNextRow from "./VideoUpNextRow.vue";

const {
  entries,
  nowPlaying = null,
  suggested = [],
  isPlaying = false,
  playingFrom = "Video library",
  contextLabel = "Video library",
} = defineProps<{
  entries: UpNextItem[];
  nowPlaying?: MediaFileDto | null;
  suggested?: MediaFileDto[];
  isPlaying?: boolean;
  playingFrom?: string;
  contextLabel?: string;
}>();

const emit = defineEmits<{
  hide: [];
  select: [file: MediaFileDto];
}>();

const store = usePlayerStore();
const { videoAutoplay } = storeToRefs(store);
const { historyByFileId } = useVideoHistory();

const queuedItems = computed(() => entries.filter((item) => item.kind === "queue"));
const contextItems = computed(() => entries.filter((item) => item.kind === "context"));

const heading = computed(() => (nowPlaying ? "Up next" : "Suggested"));
const headingCount = computed(() => (nowPlaying ? entries.length : suggested.length));

const progressFor = (file: MediaFileDto): number | null =>
  progressPercentOf(historyByFileId.value.get(file.fileId), file.duration);

const watchedFor = (file: MediaFileDto): boolean =>
  isWatchedEntry(historyByFileId.value.get(file.fileId));

const playQueued = (item: UpNextItem) => {
  void store.skipToQueue(item.queueIndex);
};

const playContext = (item: UpNextItem) => {
  void store.jumpTo(item.position);
};

const toggleCurrent = () => store.togglePlay();
const toggleAutoplay = () => store.toggleVideoAutoplay();
const clearQueue = () => store.clearQueue();
const hide = () => emit("hide");
const selectSuggested = (file: MediaFileDto) => emit("select", file);
</script>

<template>
  <div class="flex flex-col h-full min-h-0">
    <div
      class="flex items-center gap-2 px-4 py-2 border-b border-gray-200/70 dark:border-gray-700/70"
    >
      <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">{{ heading }}</span>
      <span
        class="px-2 rounded-full text-xs tabular-nums bg-black/5 dark:bg-white/5 text-gray-600 dark:text-gray-400"
      >
        {{ headingCount }}
      </span>
      <span class="flex-1" />
      <button
        type="button"
        class="w-9 h-9 rounded-lg flex items-center justify-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        :class="
          videoAutoplay
            ? 'text-primary'
            : 'text-gray-500 dark:text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'
        "
        :title="videoAutoplay ? 'Disable autoplay' : 'Enable autoplay'"
        :aria-label="videoAutoplay ? 'Disable autoplay' : 'Enable autoplay'"
        :aria-pressed="videoAutoplay"
        @click="toggleAutoplay"
      >
        <Icon icon="mdi:playlist-play" class="w-5 h-5" />
      </button>
      <button
        type="button"
        class="w-9 h-9 rounded-lg flex items-center justify-center text-gray-500 dark:text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        title="Hide up next"
        aria-label="Hide up next"
        @click="hide"
      >
        <Icon icon="mdi:close" class="w-4 h-4" />
      </button>
    </div>

    <div class="flex-1 min-h-0 overflow-y-auto px-2 py-2">
      <template v-if="nowPlaying">
        <p class="px-2 pt-1 pb-1 text-xs text-gray-500 dark:text-gray-500">Now playing</p>
        <VideoUpNextRow
          :file="nowPlaying"
          :active="true"
          :playing="isPlaying"
          :progress-percent="progressFor(nowPlaying)"
          :watched="watchedFor(nowPlaying)"
          @select="toggleCurrent"
        />
      </template>

      <template v-if="queuedItems.length > 0">
        <div class="flex items-center justify-between px-2 pt-2 pb-1">
          <p class="text-xs text-gray-500 dark:text-gray-500">Next in queue</p>
          <button
            type="button"
            class="text-xs text-gray-500 dark:text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition-colors"
            title="Clear queue"
            @click="clearQueue"
          >
            Clear
          </button>
        </div>
        <VideoUpNextRow
          v-for="item in queuedItems"
          :key="`q-${item.queueIndex}`"
          :file="item.file"
          :progress-percent="progressFor(item.file)"
          :watched="watchedFor(item.file)"
          @select="playQueued(item)"
        />
      </template>

      <template v-if="contextItems.length > 0">
        <p class="px-2 pt-2 pb-1 text-xs text-gray-500 dark:text-gray-500">
          Next from {{ contextLabel }}
        </p>
        <VideoUpNextRow
          v-for="item in contextItems"
          :key="`c-${item.position}`"
          :file="item.file"
          :progress-percent="progressFor(item.file)"
          :watched="watchedFor(item.file)"
          @select="playContext(item)"
        />
      </template>

      <template v-if="!nowPlaying">
        <VideoUpNextRow
          v-for="file in suggested"
          :key="file.fileId"
          :file="file"
          :progress-percent="progressFor(file)"
          :watched="watchedFor(file)"
          @select="selectSuggested(file)"
        />
      </template>

      <p
        v-if="nowPlaying && entries.length === 0"
        class="px-2 py-4 text-xs text-gray-500 dark:text-gray-500"
      >
        Nothing queued after this video.
      </p>
      <p
        v-if="!nowPlaying && suggested.length === 0"
        class="px-2 py-4 text-xs text-gray-500 dark:text-gray-500"
      >
        You are all caught up.
      </p>
    </div>

    <div
      class="flex items-center gap-2 px-4 py-2.5 border-t border-gray-200/70 dark:border-gray-700/70"
    >
      <Icon icon="mdi:film-open-outline" class="w-3.5 h-3.5 text-primary shrink-0" />
      <p class="text-xs text-gray-600 dark:text-gray-400 truncate">
        {{ nowPlaying ? `Playing from ${playingFrom}` : "Video library" }}
      </p>
    </div>
  </div>
</template>
