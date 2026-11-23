<script setup lang="ts">
import { useEventListener } from "@vueuse/core";
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { isNavigationFailure, useRouter } from "vue-router";

import type { MediaFileDto } from "@/api/streaming";

import { fileApi } from "@/api/file";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { usePlayerEngine } from "@/composables/usePlayerEngine";
import { usePlayerStore } from "@/stores/stream-player";
import { formatPlaybackTime } from "@/utils/date-formatters";

// The video playing from a note, shown in the note itself. It is the main player's track on a
// surface of its own, so history, resume and the media keys work as on the Videos page.
const { media, title } = defineProps<{
  media: MediaFileDto;
  title: string;
}>();

const player = usePlayerStore();
const router = useRouter();

const rootRef = ref<HTMLElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);
// No container, so Shaka draws no controls of its own over the note's.
const noShakaUi = ref<HTMLElement | null>(null);

const posterUrl = computed(() =>
  fileApi.getThumbnailUrlForVersion(
    media.fileId,
    media.playbackVersionId ?? media.currentVersionId,
    media.coverUpdatedAt,
  ),
);

const { isBuffering, loadError, resumePrompt, acceptResumePrompt, dismissResumePrompt } =
  usePlayerEngine(videoRef, noShakaUi, {
    getThumbnailUrl: () => posterUrl.value,
    mediaKind: "video",
  });

const isThisPlaying = computed(() => player.activeFile?.fileId === media.fileId);
const isPlaying = computed(() => isThisPlaying.value && player.isPlaying);

// A video started from a note ends there instead of rolling on into whatever source played
// before it.
watch(
  () => player.autoplayCountdown,
  (seconds) => {
    if (seconds !== null && isThisPlaying.value) player.cancelAutoplay();
  },
);

const togglePlay = () => {
  if (resumePrompt.value) {
    acceptResumePrompt();
    return;
  }

  player.togglePlay();
};

const playLabel = computed(() => {
  if (isPlaying.value) return "Pause";

  return "Play";
});

// The seek bar follows playback except while it is being dragged, so a drag seeks once, where
// it is let go.
const scrubTo = ref<number | null>(null);
const duration = computed(() => player.duration || media.duration || 0);
const position = computed(() => scrubTo.value ?? player.currentTime);

const onScrub = (value: number | number[] | undefined) => {
  if (typeof value === "number") scrubTo.value = value;
};

const commitScrub = () => {
  if (scrubTo.value === null) return;

  player.seek(scrubTo.value);
  scrubTo.value = null;
};

const timeLabel = computed(
  () => `${formatPlaybackTime(position.value)} / ${formatPlaybackTime(duration.value)}`,
);

const volumeBeforeMute = ref(1);
const isMuted = computed(() => player.volume === 0);

const muteIcon = computed(() => {
  if (isMuted.value) return "i-lucide-volume-x";

  return "i-lucide-volume-2";
});

const muteLabel = computed(() => {
  if (isMuted.value) return "Unmute";

  return "Mute";
});

const toggleMute = () => {
  if (isMuted.value) {
    player.setVolume(volumeBeforeMute.value || 1);
    return;
  }

  volumeBeforeMute.value = player.volume;
  player.setVolume(0);
};

const isFullscreen = ref(false);

useEventListener(document, "fullscreenchange", () => {
  isFullscreen.value = document.fullscreenElement === rootRef.value;
});

const fullscreenIcon = computed(() => {
  if (isFullscreen.value) return "i-lucide-minimize";

  return "i-lucide-maximize";
});

const fullscreenLabel = computed(() => {
  if (isFullscreen.value) return "Exit full screen";

  return "Full screen";
});

const toggleFullscreen = () => {
  if (isFullscreen.value) {
    void document.exitFullscreen();
    return;
  }

  void rootRef.value?.requestFullscreen();
};

// Leaving the note pauses the video: nothing else off the Videos page shows it yet, and it
// shouldn't carry on as sound alone. The Videos page picks it up on its own surface instead.
let isHandingOff = false;

const openInVideos = async () => {
  isHandingOff = true;

  const failure = await router.push({ name: "videos" });

  if (isNavigationFailure(failure)) isHandingOff = false;
};

onBeforeUnmount(() => {
  if (!isHandingOff && isThisPlaying.value) player.closePlayer();
});
</script>

<template>
  <!-- Pointer and key events stay here, so working the player never moves the editor's cursor
       or selects the embed. -->
  <span
    ref="rootRef"
    class="flex flex-col bg-black text-gray-100"
    :class="{ 'h-full': isFullscreen }"
    @mousedown.stop
    @keydown.stop
  >
    <span class="flex items-center gap-2 px-3 py-1.5">
      <UIcon name="i-lucide-film" class="h-4 w-4 shrink-0 text-gray-400" aria-hidden="true" />

      <span class="min-w-0 flex-1 truncate text-sm">{{ title }}</span>

      <UTooltip text="Open in Videos">
        <UButton
          icon="i-lucide-arrow-up-right"
          size="xs"
          color="neutral"
          variant="ghost"
          square
          class="justify-center text-gray-300 hover:bg-white/10"
          :aria-label="`Open ${title} in Videos`"
          @click.stop="openInVideos"
        />
      </UTooltip>
    </span>

    <span
      class="relative block w-full"
      :class="isFullscreen ? 'min-h-0 flex-1' : 'aspect-video'"
    >
      <!-- Shaka attaches to the element itself, so it carries no source of its own. -->
      <video
        ref="videoRef"
        class="h-full w-full cursor-pointer object-contain"
        :poster="posterUrl"
        playsinline
        disablepictureinpicture
        @click="togglePlay"
      />

      <span
        v-if="isBuffering && !loadError"
        class="pointer-events-none absolute inset-0 flex items-center justify-center text-gray-300"
      >
        <BlockSpinner :size="28" aria-label="Loading the video" />
      </span>

      <span
        v-if="loadError"
        class="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/70 px-6 text-center"
      >
        <UIcon name="i-lucide-circle-alert" class="h-8 w-8 text-red-400" aria-hidden="true" />
        <span class="text-sm text-gray-100">{{ loadError }}</span>
      </span>

      <span
        v-else-if="resumePrompt"
        class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-black/60"
      >
        <span class="text-sm text-gray-100">
          Resume from {{ formatPlaybackTime(resumePrompt.positionSeconds) }}?
        </span>

        <span class="flex gap-2">
          <UButton color="primary" size="sm" label="Resume" @click.stop="acceptResumePrompt" />
          <UButton
            color="neutral"
            variant="outline"
            size="sm"
            label="Start over"
            class="text-gray-100 ring-white/30 hover:bg-white/10"
            @click.stop="dismissResumePrompt"
          />
        </span>
      </span>
    </span>

    <span class="flex items-center gap-2 px-2 py-1.5">
      <UButton
        :icon="isPlaying ? 'i-lucide-pause' : 'i-lucide-play'"
        size="sm"
        color="neutral"
        variant="ghost"
        square
        class="justify-center text-gray-100 hover:bg-white/10"
        :ui="{ leadingIcon: 'icon-solid' }"
        :aria-label="playLabel"
        @click.stop="togglePlay"
      />

      <span class="shrink-0 text-xs tabular-nums text-gray-300">{{ timeLabel }}</span>

      <USlider
        :model-value="position"
        :min="0"
        :max="duration || 1"
        :step="1"
        size="xs"
        color="primary"
        :disabled="duration === 0"
        aria-label="Seek"
        class="min-w-0 flex-1"
        @update:model-value="onScrub"
        @change="commitScrub"
      />

      <UButton
        :icon="muteIcon"
        size="sm"
        color="neutral"
        variant="ghost"
        square
        class="justify-center text-gray-100 hover:bg-white/10"
        :aria-label="muteLabel"
        @click.stop="toggleMute"
      />

      <UButton
        :icon="fullscreenIcon"
        size="sm"
        color="neutral"
        variant="ghost"
        square
        class="justify-center text-gray-100 hover:bg-white/10"
        :aria-label="fullscreenLabel"
        @click.stop="toggleFullscreen"
      />
    </span>
  </span>
</template>
