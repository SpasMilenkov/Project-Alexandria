<script setup lang="ts">
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

const {
  file,
  userId,
  playback = "library",
} = defineProps<{
  file: MediaFileDto;
  userId: string;
  playback?: "library" | "single";
}>();

const player = usePlayerStore();
const settings = useSettingsStore();
const title = computed(() => file.title || file.fileName);
const isPlaying = computed(() => player.activeFile?.fileId === file.fileId && player.isPlaying);
const disabled = computed(() => player.playbackOwnerId !== userId || player.orderBusy);

const thumbnail = useFileThumbnail(() => ({
  fileId: file.fileId,
  versionId: file.playbackVersionId ?? file.currentVersionId,
  mimeType: file.mimeType,
}));

const showArtwork = computed(
  () =>
    settings.thumbnailsEnabled &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);

const play = () => {
  if (disabled.value) return;

  if (player.activeFile?.fileId === file.fileId) {
    void player.togglePlay();

    return;
  }

  if (playback === "single") {
    void player.playNow([file]);

    return;
  }

  void player.playTrackInContext(
    file,
    { isVideo: file.isVideo, playlistId: null },
    file.isVideo ? "Videos" : "Music",
  );
};
</script>

<template>
  <button
    type="button"
    :aria-label="`${isPlaying ? 'Pause' : 'Play'} ${title}`"
    :disabled="disabled"
    class="group min-w-0 rounded-xl text-left focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary disabled:cursor-wait"
    @click="play"
  >
    <span
      class="relative flex w-full items-center justify-center overflow-hidden rounded-xl border border-gray-200/70 bg-gray-100 dark:border-gray-700/70 dark:bg-gray-800"
      :class="file.isVideo ? 'aspect-video' : 'aspect-square'"
    >
      <UIcon
        v-if="!showArtwork || !thumbnail.thumbnailLoaded.value"
        :name="file.isVideo ? 'i-mdi-film-open-outline' : 'i-mdi-music-note-outline'"
        class="size-12 text-gray-500 dark:text-gray-500"
        aria-hidden="true"
      />

      <img
        v-if="showArtwork"
        :src="thumbnail.thumbnailUrl.value"
        alt=""
        loading="lazy"
        decoding="async"
        class="absolute inset-0 size-full object-cover transition-transform duration-200 group-hover:scale-105 motion-reduce:transition-none motion-reduce:group-hover:scale-100"
        :class="{ invisible: !thumbnail.thumbnailLoaded.value }"
        @load="thumbnail.onThumbnailLoad"
        @error="thumbnail.onThumbnailError"
      />

      <span
        class="absolute bottom-2 right-2 flex size-10 items-center justify-center rounded-full bg-gray-950 text-gray-100 shadow-md"
      >
        <UIcon :name="isPlaying ? 'i-mdi-pause' : 'i-mdi-play'" class="size-5" aria-hidden="true" />
      </span>
    </span>

    <span
      class="mt-4 block truncate text-sm font-medium text-gray-900 dark:text-gray-100"
      :title="title"
      >{{ title }}</span
    >

    <span
      class="mt-1 flex items-center justify-between gap-2 text-xs text-gray-600 dark:text-gray-400"
    >
      <span class="truncate">{{
        file.artist || file.album || (file.isVideo ? "Video" : "Unknown artist")
      }}</span>
      <span v-if="file.duration != null" class="shrink-0 tabular-nums">{{
        formatDuration(file.duration)
      }}</span>
    </span>
  </button>
</template>
