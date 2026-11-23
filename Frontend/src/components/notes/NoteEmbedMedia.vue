<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, nextTick, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { fileApi } from "@/api/file";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import NoteEmbedVideoPlayer from "@/components/notes/NoteEmbedVideoPlayer.vue";
import { useVideoEmbedClaim } from "@/composables/useVideoEmbedClaim";
import { getStreamingFile } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { formatPlaybackTime } from "@/utils/date-formatters";

// Audio and video in a note: what it is, and a play button that hands it to the main player, so
// history, the queue and the mini player all work as anywhere else in the app. A playing video
// turns the card into its player.
const { fileId, name, startSeconds } = defineProps<{
  fileId: string;
  name: string;
  startSeconds: number | null;
}>();

const emit = defineEmits<{
  open: [];
}>();

const player = usePlayerStore();

// 404 until the file has been converted for streaming, which the client hands back as null.
const { data: media, isPending } = useQuery(() => getStreamingFile(fileId));

const coverFailed = ref(false);

watch(
  () => fileId,
  () => {
    coverFailed.value = false;
  },
);

const coverUrl = computed(() => {
  if (!media.value || coverFailed.value) return null;

  return fileApi.getThumbnailUrlForVersion(
    media.value.fileId,
    media.value.currentVersionId,
    media.value.coverUpdatedAt,
  );
});

const title = computed(() => media.value?.title || name);

const details = computed(() => {
  const parts = [media.value?.artist, media.value?.album].filter(Boolean);

  if (media.value?.duration) parts.push(formatPlaybackTime(media.value.duration));

  return parts.join(" · ");
});

const isCurrent = computed(() => player.activeFile?.fileId === fileId);
const isPlayingHere = computed(() => isCurrent.value && player.isPlaying);

const fallbackIcon = computed(() => {
  if (media.value?.isVideo) return "i-lucide-film";

  return "i-lucide-music";
});

const playLabel = computed(() => {
  if (isPlayingHere.value) return `Pause ${title.value}`;
  if (startSeconds !== null) return `Play ${title.value} from ${formatPlaybackTime(startSeconds)}`;

  return `Play ${title.value}`;
});

const isVideo = computed(() => media.value?.isVideo ?? false);
const videoClaim = useVideoEmbedClaim();
const showsPlayer = computed(() => isVideo.value && videoClaim.isOwner.value);

// A video that is playing with nothing showing it, like one started from a timestamp link in
// this note, comes up here.
watch(
  () =>
    isVideo.value &&
    isCurrent.value &&
    player.videoSurfaces === 0 &&
    !player.playerDismissed &&
    videoClaim.isFree.value,
  (canTakeOver) => {
    if (canTakeOver) videoClaim.claim();
  },
  { immediate: true },
);

watch(isCurrent, (current, wasCurrent) => {
  if (wasCurrent && !current) videoClaim.release();
});

const playVideo = async (file: MediaFileDto) => {
  videoClaim.claim();

  // The player picks up the current track as it mounts.
  if (isCurrent.value) return;

  // Mounting the player first means the video never starts in the hidden fallback engine.
  await nextTick();
  await player.playNow([file], { startSeconds });
};

const play = () => {
  if (!media.value) return;

  if (media.value.isVideo) {
    void playVideo(media.value);
    return;
  }

  if (isCurrent.value) {
    player.togglePlay();
    return;
  }

  void player.playNow([media.value], { startSeconds });
};
</script>

<template>
  <NoteEmbedVideoPlayer v-if="showsPlayer && media" :media="media" :title="title" />

  <span v-else class="group flex items-center gap-3 p-2">
    <span
      class="relative flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-md bg-gray-500/10"
    >
      <img
        v-if="coverUrl"
        :src="coverUrl"
        alt=""
        loading="lazy"
        class="h-full w-full object-cover"
        @error="coverFailed = true"
      />

      <UIcon
        v-else
        :name="fallbackIcon"
        class="h-6 w-6 text-gray-500 dark:text-gray-500"
        aria-hidden="true"
      />
    </span>

    <span class="flex min-w-0 flex-1 flex-col gap-0.5">
      <span class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">{{ title }}</span>

      <span v-if="isPending" class="flex h-5 items-center text-gray-500">
        <BlockSpinner :size="14" aria-label="Loading track" />
      </span>

      <span v-else-if="!media" class="truncate text-xs text-gray-600 dark:text-gray-400">
        Not ready to play yet
      </span>

      <span v-else class="truncate text-xs tabular-nums text-gray-600 dark:text-gray-400">
        {{ details }}
      </span>

      <span
        v-if="startSeconds !== null"
        class="truncate text-xs tabular-nums text-gray-500 dark:text-gray-500"
      >
        Starts at {{ formatPlaybackTime(startSeconds) }}
      </span>
    </span>

    <!-- Shown on hover or keyboard focus on desktop; touch screens have no hover, so it stays. -->
    <UTooltip text="Open">
      <UButton
        icon="i-lucide-arrow-up-right"
        color="neutral"
        variant="ghost"
        square
        class="shrink-0 justify-center transition-opacity duration-150 ease-out motion-reduce:transition-none md:opacity-0 md:group-hover:opacity-100 md:group-focus-within:opacity-100"
        :aria-label="`Open ${title}`"
        @click.stop="emit('open')"
      />
    </UTooltip>

    <UButton
      :icon="isPlayingHere ? 'i-lucide-pause' : 'i-lucide-play'"
      color="primary"
      square
      class="shrink-0 justify-center"
      :ui="{ leadingIcon: 'icon-solid' }"
      :disabled="!media"
      :aria-label="playLabel"
      @click.stop="play"
    />
  </span>
</template>
