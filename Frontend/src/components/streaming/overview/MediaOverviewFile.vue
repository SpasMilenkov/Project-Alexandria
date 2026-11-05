<script setup lang="ts">
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { coverSeed } from "@/features/media/generated-cover";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

import MediaArtwork from "./MediaArtwork.vue";
import MediaEqualizer from "./MediaEqualizer.vue";

const {
  file,
  userId,
  playback = "library",
  chip = null,
} = defineProps<{
  file: MediaFileDto;
  userId: string;
  playback?: "library" | "single";
  chip?: string | null;
}>();

const player = usePlayerStore();
const title = computed(() => file.title || file.fileName);
const isActive = computed(() => player.activeFile?.fileId === file.fileId);
const isPlaying = computed(() => isActive.value && player.isPlaying);
const disabled = computed(() => player.playbackOwnerId !== userId || player.orderBusy);

const subtitle = computed(() => {
  if (file.artist) return file.artist;
  if (file.album) return file.album;
  if (file.isVideo) return "Video";

  return "Unknown artist";
});

const isActiveVideo = computed(() => isActive.value && file.isVideo);

const progress = computed(() => {
  if (isActiveVideo.value && player.duration > 0) {
    return Math.min(100, (player.currentTime / player.duration) * 100);
  }

  if (!file.isVideo || file.hasFinished) return 0;
  if (!file.duration || !file.positionSeconds) return 0;

  return Math.min(100, (file.positionSeconds / file.duration) * 100);
});

const play = () => {
  if (disabled.value) return;

  if (isActive.value) {
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
    class="group min-w-0 snap-start rounded-2xl text-left focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary disabled:cursor-wait"
    @click="play"
  >
    <MediaArtwork
      :file-id="file.fileId"
      :version-id="file.playbackVersionId ?? file.currentVersionId"
      :mime-type="file.mimeType"
      :seed="coverSeed(file)"
      zoom
      class="w-full rounded-2xl border border-gray-200/70 shadow-md transition-transform duration-200 group-hover:-translate-y-1 motion-reduce:transition-none motion-reduce:group-hover:translate-y-0 dark:border-gray-700/70"
      :class="file.isVideo ? 'aspect-video' : 'aspect-square'"
    >
      <span
        v-if="chip"
        class="absolute left-2.5 top-2.5 inline-flex min-h-6 items-center rounded-full bg-gray-950/75 px-2 text-xs font-semibold text-gray-100"
        >{{ chip }}</span
      >

      <span
        v-if="file.isVideo && file.duration"
        class="absolute bottom-3 right-2.5 inline-flex min-h-6 items-center rounded-md bg-gray-950/80 px-2 text-xs font-semibold tabular-nums text-gray-100"
        >{{ formatDuration(file.duration) }}</span
      >

      <span
        v-if="isActiveVideo"
        class="pointer-events-none absolute inset-0 bg-gray-950/30"
        aria-hidden="true"
      />

      <span
        v-if="isActiveVideo"
        class="absolute right-2.5 top-2.5 inline-flex min-h-6 items-center gap-1 rounded-full bg-primary px-2 text-xs font-semibold text-inverted shadow-md"
      >
        <UIcon
          :name="isPlaying ? 'i-mdi-play' : 'i-mdi-pause'"
          class="size-3.5"
          aria-hidden="true"
        />
        {{ isPlaying ? "Now playing" : "Paused" }}
      </span>

      <span v-if="progress > 0" class="absolute inset-x-0 bottom-0 h-1 bg-gray-950/55">
        <span class="block h-1 bg-primary" :style="{ width: `${progress}%` }" />
      </span>

      <span
        class="absolute flex size-11 items-center justify-center rounded-full bg-primary text-inverted shadow-lg transition duration-200 motion-reduce:transition-none"
        :class="[
          file.isVideo ? 'left-1/2 top-1/2 -ml-[22px] -mt-[22px]' : 'bottom-2.5 right-2.5',
          isActive && !file.isVideo
            ? 'opacity-100'
            : 'translate-y-1 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 group-focus-visible:translate-y-0 group-focus-visible:opacity-100 pointer-coarse:translate-y-0 pointer-coarse:opacity-100',
        ]"
      >
        <MediaEqualizer v-if="isActive && !file.isVideo" :playing="isPlaying" />

        <UIcon v-else-if="isPlaying" name="i-mdi-pause" class="size-5" aria-hidden="true" />

        <UIcon v-else name="i-mdi-play" class="size-5" aria-hidden="true" />
      </span>
    </MediaArtwork>

    <span
      class="mt-3 block truncate text-sm font-semibold text-gray-900 dark:text-gray-100"
      :title="title"
      >{{ title }}</span
    >

    <span
      class="mt-0.5 flex items-center justify-between gap-2 text-sm text-gray-600 dark:text-gray-400"
    >
      <span class="truncate">{{ subtitle }}</span>

      <span v-if="!file.isVideo && file.duration != null" class="shrink-0 text-xs tabular-nums">{{
        formatDuration(file.duration)
      }}</span>
    </span>
  </button>
</template>
