<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import HomePlayerRecord from "@/components/dashboard/home/widgets/HomePlayerRecord.vue";
import { useHomeWidgetPreview, useHomeWidgetThumbnail } from "@/composables/useHomeWidgetPreview";
import { HomePlayerStyle } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

const { widget } = defineProps<{ widget: HomeWidget }>();

const home = useHomeStore();
const settings = useSettingsStore();
const player = useHomeWidgetPreview()?.player ?? usePlayerStore();
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const file = computed(() => {
  if (!home.ownerId || player.playbackOwnerId !== home.ownerId) return null;

  return player.activeFile;
});

const thumbnail = useHomeWidgetThumbnail(() => ({
  fileId: file.value?.fileId ?? "",
  versionId: file.value?.playbackVersionId ?? file.value?.currentVersionId ?? "",
  mimeType: file.value?.mimeType ?? null,
}));

const showArtwork = computed(
  () =>
    file.value !== null &&
    settings.thumbnailsEnabled &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);

const artworkStyle = computed(() => {
  if (!showArtwork.value || !thumbnail.thumbnailLoaded.value) return undefined;

  return { backgroundImage: `url(${JSON.stringify(thumbnail.thumbnailUrl.value)})` };
});

const title = computed(() => file.value?.title ?? file.value?.fileName ?? "");

const subtitle = computed(() => {
  if (file.value?.isVideo) return "Video";

  return [file.value?.artist, file.value?.album].filter(Boolean).join(" · ") || "Audio";
});

const mediaIcon = computed(() => {
  if (file.value?.isVideo) return "i-mdi-film-open-outline";

  return "i-mdi-music-note-outline";
});

const duration = computed(() => {
  if (!Number.isFinite(player.duration) || player.duration <= 0) return 0;

  return player.duration;
});

const position = computed(() => {
  if (!Number.isFinite(player.currentTime) || player.currentTime < 0) return 0;
  if (duration.value) return Math.min(player.currentTime, duration.value);

  return player.currentTime;
});

const progress = computed(() => {
  if (!duration.value) return 0;

  return (position.value / duration.value) * 100;
});

const failure = computed(() => player.engineLoadError ?? player.error?.message ?? null);

const disabled = computed(
  () =>
    !streamingEnabled || !file.value || home.isEditing || player.orderBusy || !player.engineReady,
);

const loading = computed(
  () => !failure.value && (!player.engineReady || player.orderBusy || player.engineBuffering),
);

const playLabel = computed(() => (player.isPlaying ? "Pause" : "Play"));
const spinning = computed(() => player.isPlaying && !player.engineBuffering && !failure.value);

const control = (action: "togglePlay" | "previous" | "next") => {
  if (disabled.value) return;
  if (action === "previous" && !player.hasPrevious) return;
  if (action === "next" && !player.hasNext) return;

  void player[action]();
};

const seek = (event: Event) => {
  if (
    disabled.value ||
    failure.value ||
    !duration.value ||
    !(event.target instanceof HTMLInputElement)
  )
    return;

  const value = event.target.valueAsNumber;

  if (Number.isFinite(value)) player.seek(Math.max(0, Math.min(duration.value, value)));
};
</script>

<template>
  <div
    v-if="streamingEnabled && file"
    class="player-face relative isolate flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden rounded-xl text-white dark:text-white"
    :class="{
      'pointer-events-none': home.isEditing,
      'player-face--vinyl': widget.options.playerStyle === HomePlayerStyle.Vinyl,
    }"
    data-home-player
    :data-player-style="widget.options.playerStyle"
  >
    <div class="player-tint pointer-events-none absolute inset-0 -z-10" :style="artworkStyle" />
    <div class="player-shade pointer-events-none absolute inset-0 -z-10" />

    <HomePlayerRecord
      v-if="widget.options.playerStyle === HomePlayerStyle.Vinyl"
      class="player-artwork shrink-0"
      :src="showArtwork ? thumbnail.thumbnailUrl.value : null"
      :loaded="thumbnail.thumbnailLoaded.value"
      :spinning="spinning"
      :icon="mediaIcon"
      @load="thumbnail.onThumbnailLoad"
      @error="thumbnail.onThumbnailError"
    />

    <div v-else class="player-artwork relative shrink-0 overflow-hidden rounded-xl bg-white/10">
      <UIcon
        :name="mediaIcon"
        class="absolute inset-0 m-auto size-12 text-gray-200 dark:text-gray-200"
        aria-hidden="true"
      />

      <img
        v-if="showArtwork"
        :src="thumbnail.thumbnailUrl.value"
        alt=""
        decoding="async"
        class="absolute inset-0 h-full w-full object-cover"
        :class="{ invisible: !thumbnail.thumbnailLoaded.value }"
        @load="thumbnail.onThumbnailLoad"
        @error="thumbnail.onThumbnailError"
      />
    </div>

    <div class="player-details relative flex min-h-0 min-w-0 flex-col gap-2">
      <div class="player-track min-w-0">
        <p class="truncate text-sm font-semibold" :title="title">{{ title }}</p>
        <p
          class="player-subtitle truncate text-xs text-gray-200 dark:text-gray-200"
          :title="subtitle"
        >
          {{ subtitle }}
        </p>
      </div>

      <div v-if="!failure" class="player-progress">
        <input
          type="range"
          min="0"
          :max="duration || 1"
          step="1"
          :value="position"
          :disabled="disabled || Boolean(failure) || !duration"
          :tabindex="home.isEditing ? -1 : undefined"
          aria-label="Seek playback"
          :aria-valuetext="`${formatDuration(position)} of ${duration ? formatDuration(duration) : 'unknown duration'}`"
          class="player-seek block w-full"
          :style="{ '--player-progress': `${progress}%` }"
          @input="seek"
        />

        <div
          class="player-times flex justify-between gap-2 text-xs tabular-nums text-gray-200 dark:text-gray-200"
        >
          <span>{{ formatDuration(position) }}</span>
          <span>{{ duration ? formatDuration(duration) : "--:--" }}</span>
        </div>
      </div>

      <div class="player-controls flex items-center justify-center gap-4">
        <UButton
          color="neutral"
          variant="ghost"
          icon="i-mdi-skip-previous"
          aria-label="Previous"
          :disabled="disabled || !player.hasPrevious"
          class="player-skip size-10 shrink-0 justify-center rounded-full text-white hover:bg-white/10 dark:text-white dark:hover:bg-white/10"
          @click="control('previous')"
        />

        <UButton
          color="neutral"
          variant="solid"
          :icon="player.isPlaying ? 'i-mdi-pause' : 'i-mdi-play'"
          :loading="loading"
          :aria-label="playLabel"
          :disabled="disabled"
          class="size-10 shrink-0 justify-center rounded-full bg-white text-gray-900 hover:bg-gray-200 dark:bg-white dark:text-gray-900 dark:hover:bg-gray-200"
          @click="control('togglePlay')"
        />

        <UButton
          color="neutral"
          variant="ghost"
          icon="i-mdi-skip-next"
          aria-label="Next"
          :disabled="disabled || !player.hasNext"
          class="player-skip size-10 shrink-0 justify-center rounded-full text-white hover:bg-white/10 dark:text-white dark:hover:bg-white/10"
          @click="control('next')"
        />
      </div>

      <p
        v-if="failure"
        role="alert"
        class="player-error truncate text-xs text-gray-200 dark:text-gray-200"
        :title="failure"
      >
        {{ failure }}
      </p>
    </div>
  </div>

  <div
    v-else
    class="player-empty flex min-h-0 min-w-0 flex-1 flex-col items-center justify-center gap-2 text-center"
  >
    <UIcon
      name="i-mdi-music-note-outline"
      class="player-empty-icon size-8 text-gray-400 dark:text-gray-600"
      aria-hidden="true"
    />
    <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
      {{ streamingEnabled ? "Nothing playing" : "Streaming is unavailable" }}
    </p>
    <p
      v-if="streamingEnabled"
      class="player-empty-description text-sm text-gray-600 dark:text-gray-400"
    >
      Start music or a video from your library.
    </p>

    <div
      v-if="streamingEnabled"
      class="flex flex-wrap justify-center gap-2"
      :class="{ 'pointer-events-none': home.isEditing }"
    >
      <UButton to="/streaming/music" color="neutral" variant="outline" :disabled="home.isEditing"
        >Music</UButton
      >
      <UButton to="/streaming/videos" color="neutral" variant="outline" :disabled="home.isEditing"
        >Videos</UButton
      >
    </div>
  </div>
</template>

<style scoped>
.player-face {
  gap: 1rem;
  padding: 0.75rem;
  justify-content: center;
  background: linear-gradient(135deg, #293047, #111827);
}

.player-tint {
  background-position: center;
  background-size: cover;
  filter: blur(8px);
  transform: scale(1.15);
}

.player-shade {
  background: linear-gradient(135deg, rgb(15 23 42 / 72%), rgb(3 7 18 / 84%));
}

.player-artwork {
  display: none;
  width: min(88%, 24rem, max(3rem, calc(100cqh - var(--player-header-space, 0px) - 12rem)));
  aspect-ratio: 1;
  align-self: center;
}

.player-times,
.player-skip {
  display: none;
}

.player-seek {
  appearance: none;
  height: 1.5rem;
  background: transparent;
  cursor: pointer;
}

.player-seek:disabled {
  cursor: default;
}

.player-seek:focus-visible {
  outline: 2px solid white;
  outline-offset: 2px;
  border-radius: 0.5rem;
}

.player-seek::-webkit-slider-runnable-track {
  height: 4px;
  border-radius: 999px;
  background: linear-gradient(
    to right,
    white var(--player-progress),
    rgb(255 255 255 / 30%) var(--player-progress)
  );
}

.player-seek::-moz-range-track {
  height: 4px;
  border-radius: 999px;
  background: rgb(255 255 255 / 30%);
}

.player-seek::-moz-range-progress {
  height: 4px;
  border-radius: 999px;
  background: white;
}

.player-seek::-webkit-slider-thumb {
  appearance: none;
  width: 12px;
  height: 12px;
  margin-top: -4px;
  border-radius: 50%;
  background: white;
}

.player-seek::-moz-range-thumb {
  width: 12px;
  height: 12px;
  border: 0;
  border-radius: 50%;
  background: white;
}

@container home-widget (min-width: 220px) {
  .player-skip {
    display: inline-flex;
  }
}

@container home-widget (min-height: 224px) {
  .player-times {
    display: flex;
  }
}

@container home-widget (min-height: 300px) {
  .player-artwork {
    display: block;
  }
}

@container home-widget (min-aspect-ratio: 1.7 / 1) and (min-width: 360px) {
  .player-face {
    flex-direction: row;
    align-items: center;
  }

  .player-artwork {
    display: block;
    width: min(38%, 26rem, max(3rem, calc(100cqh - var(--player-header-space, 0px) - 2.5rem)));
  }

  .player-details {
    flex: 1;
  }
}

@container home-widget (max-height: 224px) {
  .player-face {
    gap: 0.5rem;
    padding: 0.5rem;
  }

  .player-subtitle,
  .player-empty-icon,
  .player-empty-description {
    display: none;
  }

  .player-details {
    gap: 0.25rem;
  }

  .player-seek {
    height: 1rem;
  }
}

@container home-widget (max-height: 176px) and (max-width: 359px) {
  .player-face {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    grid-template-rows: auto minmax(0, 1fr) auto;
    align-items: center;
    column-gap: 0.5rem;
    row-gap: 0.25rem;
  }

  .player-artwork {
    display: block;
    grid-column: 1;
    grid-row: 2;
    justify-self: start;
    width: auto;
    height: 100%;
    max-width: 100%;
    max-height: 4rem;
  }

  .player-details {
    display: contents;
  }

  .player-track {
    grid-column: 1 / -1;
    grid-row: 1;
  }

  .player-controls {
    grid-column: 2;
    grid-row: 2;
  }

  .player-skip {
    display: none;
  }

  .player-progress,
  .player-error {
    grid-column: 1 / -1;
    grid-row: 3;
  }

  .player-face--vinyl .player-artwork {
    position: absolute;
    grid-area: auto;
    left: 0.5rem;
    top: 50%;
    width: min(calc(100% - 4rem), calc(100cqh - var(--player-header-space, 0px) - 1rem));
    height: auto;
    max-width: none;
    max-height: none;
    transform: translateY(-50%);
  }

  .player-face--vinyl::after {
    content: "";
    position: absolute;
    inset: 0;
    pointer-events: none;
    background: linear-gradient(
      to bottom,
      rgb(3 7 18 / 65%),
      transparent 35% 65%,
      rgb(3 7 18 / 65%)
    );
  }

  .player-face--vinyl :is(.player-track, .player-controls, .player-progress, .player-error) {
    position: relative;
    z-index: 1;
  }
}
</style>
