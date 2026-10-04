<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { formatDuration } from "@/utils/date-formatters";

const { file, positionSeconds } = defineProps<{
  file: MediaFileDto;
  positionSeconds: number | null;
}>();

const emit = defineEmits<{
  resume: [];
  restart: [];
}>();

const { thumbnailUrl, thumbnailLoaded, thumbnailErrored, onThumbnailLoad, onThumbnailError } =
  useFileThumbnail(() => ({
    fileId: file.fileId,
    versionId: file.currentVersionId,
  }));

const showArtwork = computed(() => !thumbnailErrored.value);
const showSpinner = computed(() => !thumbnailLoaded.value && !thumbnailErrored.value);

const displayName = computed(() => file.title ?? file.fileName);

const headingLabel = computed(() => {
  if (positionSeconds !== null) return "Continue watching";
  return "Suggested";
});

const hasProgress = computed(() => positionSeconds !== null);

const progressPercent = computed(() => {
  if (!file.duration || file.duration <= 0) return 0;
  if (positionSeconds === null) return 0;
  return Math.min(100, Math.max(0, (positionSeconds / file.duration) * 100));
});

const remainingText = computed(() => {
  if (!file.duration || file.duration <= 0) return null;
  if (positionSeconds === null) return null;
  const remaining = Math.max(0, Math.round(file.duration - positionSeconds));
  if (remaining >= 3600) {
    const hours = Math.floor(remaining / 3600);
    const minutes = Math.round((remaining % 3600) / 60);
    return `${hours}h ${minutes}m left`;
  }
  return `${Math.max(1, Math.round(remaining / 60))} min left`;
});

const metaText = computed(() => {
  if (positionSeconds === null) {
    if (file.duration) return formatDuration(file.duration);
    return file.artist ?? "Video";
  }
  const stopped = `stopped at ${formatDuration(positionSeconds)}`;
  if (remainingText.value) return `${remainingText.value}, ${stopped}`;
  return stopped;
});

const onResume = () => emit("resume");
const onRestart = () => emit("restart");
</script>

<template>
  <div class="video-hero absolute inset-0 flex items-center gap-4 md:gap-12 px-4 md:px-[6%]">
    <div class="flex-1 min-w-0 max-w-xl">
      <p class="text-sm text-gray-300 mb-1.5">{{ headingLabel }}</p>
      <h2 class="hero-title text-white">{{ displayName }}</h2>
      <p class="text-sm text-gray-300 mt-2 mb-5">{{ metaText }}</p>
      <div
        v-if="file.duration && hasProgress"
        class="h-1 rounded-full bg-white/20 overflow-hidden mb-5"
        role="progressbar"
        :aria-valuenow="Math.round(progressPercent)"
        aria-valuemin="0"
        aria-valuemax="100"
        :aria-label="`${Math.round(progressPercent)} percent watched`"
      >
        <div class="h-full bg-primary rounded-full" :style="{ width: `${progressPercent}%` }" />
      </div>
      <div class="flex gap-2 flex-wrap">
        <button type="button" class="hero-btn hero-btn-primary" @click="onResume">
          <Icon icon="mdi:play" class="w-4 h-4" />
          {{ hasProgress ? "Resume" : "Play" }}
        </button>
        <button
          v-if="hasProgress"
          type="button"
          class="hero-btn hero-btn-secondary"
          @click="onRestart"
        >
          Start over
        </button>
      </div>
    </div>
    <button
      type="button"
      class="hero-card hidden sm:grid"
      :aria-label="`${headingLabel} ${displayName}`"
      @click="onResume"
    >
      <img
        v-if="showArtwork"
        :src="thumbnailUrl"
        :alt="displayName"
        loading="lazy"
        class="absolute inset-0 w-full h-full object-cover"
        :class="{ 'opacity-0': showSpinner }"
        @load="onThumbnailLoad"
        @error="onThumbnailError"
      />
      <span
        v-if="thumbnailErrored"
        class="absolute inset-0 flex items-center justify-center"
        aria-hidden="true"
      >
        <Icon icon="mdi:file-video" class="w-10 h-10 text-white/60" />
      </span>
      <span class="hero-play" aria-hidden="true">
        <Icon icon="mdi:play" class="w-6 h-6" />
      </span>
    </button>
    <div class="hero-keys hidden md:flex" aria-hidden="true">
      <span><kbd>Space</kbd>Play or pause</span>
      <span><kbd>F</kbd>Fullscreen</span>
      <span><kbd>N</kbd>Next</span>
    </div>
  </div>
</template>

<style scoped>
.video-hero {
  background: radial-gradient(70% 90% at 85% 40%, rgba(0, 0, 0, 0.5), transparent 70%);
}
.hero-title {
  font-size: clamp(24px, 3.4vw, 40px);
  line-height: 1.1;
  margin: 0;
  font-weight: 700;
  letter-spacing: -0.02em;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
}
.hero-btn {
  height: 40px;
  padding: 0 16px;
  border-radius: 10px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition:
    background-color 150ms ease,
    filter 150ms ease;
}
.hero-btn:focus-visible {
  outline: 2px solid var(--ui-primary);
  outline-offset: 2px;
}
.hero-btn-primary {
  background: var(--ui-primary);
  color: #fff;
  border-radius: var(--ui-radius);
}
.hero-btn-primary:hover {
  filter: brightness(1.1);
}
.hero-btn-secondary {
  border: 1px solid rgba(255, 255, 255, 0.25);
  color: #fff;
  background: transparent;
}
.hero-btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
}
.hero-card {
  position: relative;
  flex: 0 0 min(44%, 520px);
  aspect-ratio: 16 / 9;
  border-radius: 14px;
  border: 1px solid rgba(255, 255, 255, 0.14);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.5);
  overflow: hidden;
  place-items: center;
  cursor: pointer;
  background: #1a1a1e;
  transition: transform 200ms ease-out;
}
.hero-card:hover {
  transform: scale(1.015);
}
.hero-card:focus-visible {
  outline: 2px solid var(--ui-primary);
  outline-offset: 2px;
}
.hero-play {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.45);
  display: grid;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: #fff;
  position: relative;
  z-index: 1;
}
.hero-keys {
  position: absolute;
  left: 6%;
  bottom: 16px;
  gap: 16px;
  color: #9aa1ae;
  font-size: 12px;
}
.hero-keys kbd {
  font-family: inherit;
  font-size: 11px;
  padding: 1px 6px;
  border: 1px solid rgba(255, 255, 255, 0.25);
  border-radius: 5px;
  margin-right: 6px;
  color: #d1d5db;
}
</style>
