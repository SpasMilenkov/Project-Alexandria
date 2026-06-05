<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { useDark } from "@vueuse/core";
import { storeToRefs } from "pinia";
import { computed, onMounted, onUnmounted, ref } from "vue";

import { VIDEO_SHAKA_UI_CONFIG, usePlayerEngine } from "@/composables/usePlayerEngine";
import { getPreview } from "@/queries/files";
import { usePlayerStore } from "@/stores/stream-player";

import PlayerSettings from "./PlayerSettings.vue";

const store = usePlayerStore();
const {
  activeFile,
  hasNext,
  hasPrevious,
  repeatMode,
  shuffled,
  isPlaying,
  currentTime,
  duration,
  videoAutoplay,
  autoplayCountdown,
  volume,
} = storeToRefs(store);

const isDark = useDark();

const rootRef = ref<HTMLDivElement | null>(null);
const containerRef = ref<HTMLDivElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);

const { data: preview } = useQuery(() =>
  activeFile.value?.fileId
    ? getPreview(activeFile.value.fileId)
    : { key: ["preview", "none"], query: () => Promise.resolve(null) },
);
const thumbnailUrl = computed(() => preview.value?.thumbnailUrl ?? null);

const { isBuffering, loadError, resumePrompt, acceptResumePrompt, dismissResumePrompt } =
  usePlayerEngine(videoRef, containerRef, {
    getThumbnailUrl: () => thumbnailUrl.value,
    shakaUiConfig: {
      ...VIDEO_SHAKA_UI_CONFIG,
      controlPanelElements: [],
      overflowMenuButtons: [],
    },
  });

const hasFile = computed(() => activeFile.value !== null);
const displayTitle = computed(() => activeFile.value?.title ?? activeFile.value?.fileName ?? null);
const displayArtist = computed(() => activeFile.value?.artist ?? null);

const handlePlayToggle = () => {
  if (resumePrompt.value) {
    acceptResumePrompt();
    return;
  }
  store.togglePlay();
};

const formattedTime = (seconds: number) => {
  if (!isFinite(seconds) || isNaN(seconds)) return "0:00";
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, "0")}`;
};

const progressPercent = computed(() => {
  if (!duration.value) return 0;
  return (currentTime.value / duration.value) * 100;
});

const onProgressClick = (e: MouseEvent) => {
  const bar = e.currentTarget as HTMLElement;
  const rect = bar.getBoundingClientRect();
  const frac = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
  store.seek(frac * (duration.value ?? 0));
};

// Fullscreen

const isFullscreen = ref(false);

const toggleFullscreen = () => {
  if (!document.fullscreenElement) {
    rootRef.value?.requestFullscreen();
  } else {
    document.exitFullscreen();
  }
};

const onFullscreenChange = () => {
  isFullscreen.value = !!document.fullscreenElement;
};

// Volume

const premuteVolume = ref(1);
const isMuted = computed(() => volume.value === 0);

const toggleMute = () => {
  if (!videoRef.value) return;
  if (isMuted.value) {
    const restore = premuteVolume.value > 0 ? premuteVolume.value : 1;
    videoRef.value.volume = restore;
    store.setVolume(restore);
  } else {
    premuteVolume.value = volume.value;
    videoRef.value.volume = 0;
    store.setVolume(0);
  }
};

const onVolumeInput = (e: Event) => {
  const v = Number((e.target as HTMLInputElement).value);
  if (videoRef.value) videoRef.value.volume = v;
  store.setVolume(v);
};

const volumeIcon = computed(() => {
  if (volume.value === 0) return "mdi:volume-off";
  if (volume.value < 0.5) return "mdi:volume-medium";
  return "mdi:volume-high";
});

// Autoplay countdown ring

const RING_CIRCUMFERENCE = 2 * Math.PI * 16;
const AUTOPLAY_SECONDS = 5;

const countdownDashoffset = computed(() => {
  if (autoplayCountdown.value === null) return RING_CIRCUMFERENCE;
  const fraction = autoplayCountdown.value / AUTOPLAY_SECONDS;
  return RING_CIRCUMFERENCE * (1 - fraction);
});

// Lifecycle

onMounted(() => {
  document.addEventListener("fullscreenchange", onFullscreenChange);
  if (store.isAudio) store.activeFile = null;
});

onUnmounted(() => {
  document.removeEventListener("fullscreenchange", onFullscreenChange);
});
</script>

<template>
  <div ref="rootRef" class="vps-root" :class="isDark ? 'vps-dark' : 'vps-light'">
    <!-- Video area -->
    <div class="vps-video-area">
      <!-- No-file placeholder -->
      <Transition name="vps-fade">
        <div v-if="!hasFile" class="vps-placeholder">
          <Icon icon="mdi:play-circle-outline" class="w-16 h-16" style="opacity: 0.3" />
          <p>Select a video below to begin</p>
        </div>
      </Transition>

      <!-- Buffering spinner -->
      <Transition name="vps-fade">
        <div v-if="isBuffering && hasFile" class="vps-spinner-wrap">
          <div class="vps-spinner" />
        </div>
      </Transition>

      <!-- Error -->
      <Transition name="vps-fade">
        <div v-if="loadError" class="vps-error">
          <Icon icon="mdi:alert-circle-outline" class="w-10 h-10" style="color: #f87171" />
          <p>{{ loadError }}</p>
        </div>
      </Transition>

      <!-- Resume prompt -->
      <Transition name="vps-fade">
        <div v-if="resumePrompt" class="vps-resume-prompt">
          <p class="vps-resume-label">
            Resume from {{ formattedTime(resumePrompt.positionSeconds) }}?
          </p>
          <div class="vps-resume-actions">
            <button class="vps-resume-btn vps-resume-btn-primary" @click="acceptResumePrompt">
              Resume
            </button>
            <button class="vps-resume-btn vps-resume-btn-secondary" @click="dismissResumePrompt">
              Start over
            </button>
          </div>
        </div>
      </Transition>

      <!-- Autoplay countdown banner -->
      <Transition name="vps-slide-up">
        <div v-if="autoplayCountdown !== null" class="vps-autoplay-banner">
          <div class="vps-autoplay-ring">
            <svg viewBox="0 0 40 40" width="40" height="40">
              <circle cx="20" cy="20" r="16" class="vps-ring-track" />
              <circle
                cx="20"
                cy="20"
                r="16"
                class="vps-ring-progress"
                :style="{ strokeDashoffset: countdownDashoffset }"
              />
            </svg>
            <span class="vps-autoplay-count">{{ autoplayCountdown }}</span>
          </div>
          <span class="vps-autoplay-label">Up next in {{ autoplayCountdown }}s</span>
          <button class="vps-autoplay-cancel" @click="store.cancelAutoplay()">Cancel</button>
          <button class="vps-autoplay-now" @click="store.next()">Play now</button>
        </div>
      </Transition>

      <!-- Shaka container -->
      <div ref="containerRef" class="vps-shaka-container" data-shaka-player-container>
        <video
          ref="videoRef"
          class="vps-video"
          data-shaka-player
          playsinline
          disablepictureinpicture
        />
      </div>
    </div>

    <!-- Control bar -->
    <div class="vps-controls">
      <!-- Seek bar -->
      <div class="vps-seek" @click="onProgressClick">
        <div class="vps-seek-track">
          <div class="vps-seek-played" :style="{ width: `${progressPercent}%` }" />
        </div>
        <div class="vps-seek-thumb" :style="{ left: `${progressPercent}%` }" />
      </div>

      <!-- Main row -->
      <div class="vps-row">
        <!-- Time -->
        <span class="vps-time">
          {{ formattedTime(currentTime) }} / {{ formattedTime(duration) }}
        </span>

        <!-- Transport -->
        <div class="vps-transport">
          <button
            class="vps-btn"
            :disabled="!hasPrevious"
            title="Previous"
            @click="store.previous()"
          >
            <Icon icon="mdi:skip-previous" class="w-5 h-5" />
          </button>
          <button
            class="vps-btn vps-btn-play"
            :title="isPlaying ? 'Pause' : 'Play'"
            @click="handlePlayToggle"
          >
            <Icon :icon="isPlaying ? 'mdi:pause' : 'mdi:play'" class="w-6 h-6" />
          </button>
          <button class="vps-btn" :disabled="!hasNext" title="Next" @click="store.next()">
            <Icon icon="mdi:skip-next" class="w-5 h-5" />
          </button>
        </div>

        <div class="vps-spacer" />

        <!-- Secondary controls -->
        <button
          class="vps-btn"
          :class="shuffled ? 'vps-btn-active' : ''"
          title="Shuffle"
          @click="store.toggleShuffle()"
        >
          <Icon icon="mdi:shuffle-variant" class="w-5 h-5" />
        </button>
        <button
          class="vps-btn"
          :class="repeatMode !== 'off' ? 'vps-btn-active' : ''"
          title="Repeat"
          @click="store.toggleLoop()"
        >
          <Icon :icon="repeatMode === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'" class="w-5 h-5" />
        </button>
        <button
          class="vps-btn"
          :class="videoAutoplay ? 'vps-btn-active' : ''"
          title="Autoplay"
          @click="store.toggleVideoAutoplay()"
        >
          <Icon icon="mdi:playlist-play" class="w-5 h-5" />
        </button>

        <!-- Volume -->
        <button class="vps-btn" :title="isMuted ? 'Unmute' : 'Mute'" @click="toggleMute">
          <Icon :icon="volumeIcon" class="w-5 h-5" />
        </button>
        <input
          type="range"
          min="0"
          max="1"
          step="0.02"
          :value="volume"
          :style="{ '--vps-vol': `${volume * 100}%` }"
          class="vps-volume-slider"
          title="Volume"
          @input="onVolumeInput"
        />

        <!-- Fullscreen -->
        <button
          class="vps-btn"
          :title="isFullscreen ? 'Exit fullscreen' : 'Fullscreen'"
          @click="toggleFullscreen"
        >
          <Icon :icon="isFullscreen ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'" class="w-5 h-5" />
        </button>

        <PlayerSettings />
      </div>

      <!-- Track info -->
      <Transition name="vps-fade">
        <div v-if="displayTitle" class="vps-info">
          <span class="vps-title">{{ displayTitle }}</span>
          <span v-if="displayArtist" class="vps-artist">{{ displayArtist }}</span>
        </div>
      </Transition>
    </div>
  </div>
</template>

<style>
@import "shaka-player/dist/controls.css";

/* Root */
.vps-root {
  position: relative; /* anchor for fullscreen overlay controls */
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 0.75rem;
  overflow: hidden;
}

/* Video area */
.vps-video-area {
  position: relative;
  width: 100%;
  /*
   * Responsive height: grow with width (16:9) but never exceed 65 vh.
   * Using min() keeps the video from becoming a thin strip on landscape
   * phones or short browser windows — the video stays proportional to
   * whichever axis is the constraint.
   */
  height: min(56.25vw, 65vh);
  background: #000;
  overflow: hidden;
  flex-shrink: 0;
  flex: 1;
}

/* Fullscreen  */
/*
 * In fullscreen the video area expands to cover the entire root.
 * The control bar becomes a gradient overlay pinned to the bottom,
 * so the video always fills 100 % of the screen — no black void.
 */
.vps-root:fullscreen,
.vps-root:-webkit-full-screen {
  border-radius: 0;
  /* root itself fills the viewport — browser guarantees this in fullscreen */
}

.vps-root:fullscreen .vps-video-area,
.vps-root:-webkit-full-screen .vps-video-area {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  flex-shrink: unset;
}

.vps-root:fullscreen .vps-controls,
.vps-root:-webkit-full-screen .vps-controls {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 30;
  /* gradient so controls are readable over any video frame */
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.92) 0%,
    rgba(0, 0, 0, 0.55) 55%,
    transparent 100%
  ) !important;
  border-top: none !important;
  padding-bottom: 1.5rem;
}

/* Force white palette on controls regardless of light/dark theme */
.vps-root:fullscreen .vps-btn,
.vps-root:-webkit-full-screen .vps-btn {
  color: rgba(255, 255, 255, 0.55) !important;
}
.vps-root:fullscreen .vps-btn:hover:not(:disabled),
.vps-root:-webkit-full-screen .vps-btn:hover:not(:disabled) {
  color: #fff !important;
  background: rgba(255, 255, 255, 0.1) !important;
}
.vps-root:fullscreen .vps-time,
.vps-root:-webkit-full-screen .vps-time {
  color: rgba(255, 255, 255, 0.55) !important;
}
.vps-root:fullscreen .vps-seek-track,
.vps-root:-webkit-full-screen .vps-seek-track {
  background: rgba(255, 255, 255, 0.2) !important;
}
.vps-root:fullscreen .vps-title,
.vps-root:-webkit-full-screen .vps-title {
  color: rgba(255, 255, 255, 0.9) !important;
}
.vps-root:fullscreen .vps-artist,
.vps-root:-webkit-full-screen .vps-artist {
  color: rgba(255, 255, 255, 0.45) !important;
}
.vps-root:fullscreen .vps-volume-slider,
.vps-root:-webkit-full-screen .vps-volume-slider {
  /* unset any light theme override */
  background: linear-gradient(
    to right,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol, 100%),
    rgba(255, 255, 255, 0.2) var(--vps-vol, 100%)
  ) !important;
}

/* Shaka container */
.vps-shaka-container {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

/*
 * Force Shaka's wrapper divs to fill their container so the video element
 * always has a real bounding box to scale into.
 */
.vps-shaka-container .shaka-video-container {
  width: 100% !important;
  height: 100% !important;
  background: transparent !important;
}

.vps-video {
  width: 100%;
  height: 100%;
  display: block;
  /*
   * object-fit: contain keeps the native aspect ratio and adds letterbox
   * bars rather than stretching — correct behaviour at every window size
   * and in fullscreen.
   */
  object-fit: contain;
}

/* Shaka UI overrides */
.vps-shaka-container .shaka-controls-container {
  z-index: 10 !important;
}

.vps-shaka-container .shaka-bottom-controls {
  display: none !important;
}

.vps-shaka-container .shaka-play-button {
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.2);
  backdrop-filter: blur(8px);
  transition:
    background 150ms ease,
    transform 150ms ease;
}

.vps-shaka-container .shaka-play-button:hover {
  background: rgba(255, 255, 255, 0.28);
  transform: scale(1.05);
}

.vps-shaka-container .shaka-overflow-menu,
.vps-shaka-container .shaka-settings-menu {
  position: absolute !important;
}

/* Control bar */
.vps-controls {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  padding: 0.75rem 1rem;
  border-top: 1px solid;
  flex-shrink: 0;
}

.vps-dark .vps-controls {
  background: #0a0a0a;
  border-color: rgba(255, 255, 255, 0.1);
}

.vps-light .vps-controls {
  background: #fff;
  border-color: rgba(0, 0, 0, 0.1);
}

/* Seek bar */
.vps-seek {
  position: relative;
  width: 100%;
  height: 1.25rem;
  display: flex;
  align-items: center;
  cursor: pointer;
}

.vps-seek-track {
  position: relative;
  width: 100%;
  height: 4px;
  border-radius: 2px;
  overflow: hidden;
  transition: height 100ms ease;
}

.vps-dark .vps-seek-track {
  background: rgba(255, 255, 255, 0.12);
}
.vps-light .vps-seek-track {
  background: rgba(0, 0, 0, 0.12);
}

.vps-seek-played {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  background: var(--ui-primary, #6366f1);
  border-radius: 2px;
  transition: width 80ms linear;
}

.vps-seek-thumb {
  position: absolute;
  top: 50%;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
  transform: translate(-50%, -50%);
  opacity: 0;
  transition: opacity 120ms ease;
  pointer-events: none;
}

.vps-seek:hover .vps-seek-thumb {
  opacity: 1;
}
.vps-seek:hover .vps-seek-track {
  height: 5px;
}

/* Row */
.vps-row {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.vps-spacer {
  flex: 1;
}

.vps-time {
  font-size: 0.75rem;
  font-variant-numeric: tabular-nums;
  flex-shrink: 0;
  margin-right: 0.25rem;
}

.vps-dark .vps-time {
  color: rgba(255, 255, 255, 0.45);
}
.vps-light .vps-time {
  color: rgba(0, 0, 0, 0.45);
}

.vps-transport {
  display: flex;
  align-items: center;
  gap: 0.125rem;
}

/* Buttons */
.vps-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 0.5rem;
  flex-shrink: 0;
  transition:
    color 120ms ease,
    background 120ms ease;
}

.vps-dark .vps-btn {
  color: rgba(255, 255, 255, 0.45);
}
.vps-light .vps-btn {
  color: rgba(0, 0, 0, 0.45);
}

.vps-dark .vps-btn:hover:not(:disabled) {
  color: rgba(255, 255, 255, 0.9);
  background: rgba(255, 255, 255, 0.08);
}

.vps-light .vps-btn:hover:not(:disabled) {
  color: rgba(0, 0, 0, 0.85);
  background: rgba(0, 0, 0, 0.06);
}

.vps-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.vps-btn-play {
  width: 2.5rem;
  height: 2.5rem;
}

.vps-btn-active {
  color: var(--ui-primary, #6366f1) !important;
}

/* Volume slider */
.vps-volume-slider {
  --vps-vol: 100%;
  width: 72px;
  height: 4px;
  border-radius: 2px;
  cursor: pointer;
  appearance: none;
  -webkit-appearance: none;
  outline: none;
  flex-shrink: 0;
  transition: width 120ms ease;
}

.vps-volume-slider:hover {
  width: 88px;
}

.vps-dark .vps-volume-slider {
  background: linear-gradient(
    to right,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(255, 255, 255, 0.15) var(--vps-vol)
  );
}

.vps-light .vps-volume-slider {
  background: linear-gradient(
    to right,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(0, 0, 0, 0.12) var(--vps-vol)
  );
}

.vps-volume-slider::-webkit-slider-thumb {
  appearance: none;
  -webkit-appearance: none;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
  cursor: pointer;
  opacity: 0;
  transition: opacity 120ms ease;
}

.vps-volume-slider:hover::-webkit-slider-thumb {
  opacity: 1;
}

.vps-volume-slider::-moz-range-thumb {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
  cursor: pointer;
  border: none;
  opacity: 0;
  transition: opacity 120ms ease;
}

.vps-volume-slider:hover::-moz-range-thumb {
  opacity: 1;
}

/* Overlay prompts */
.vps-placeholder,
.vps-spinner-wrap,
.vps-error,
.vps-resume-prompt {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  z-index: 5;
  pointer-events: none;
}

.vps-placeholder {
  color: rgba(255, 255, 255, 0.3);
  font-size: 0.875rem;
  background: #0a0a0a;
}

.vps-error {
  background: rgba(0, 0, 0, 0.7);
  color: #fca5a5;
  font-size: 0.875rem;
}

.vps-spinner {
  width: 3rem;
  height: 3rem;
  border-radius: 50%;
  border: 2px solid rgba(255, 255, 255, 0.15);
  border-top-color: #fff;
  animation: vps-spin 0.8s linear infinite;
}

@keyframes vps-spin {
  to {
    transform: rotate(360deg);
  }
}

/* Resume prompt */
.vps-resume-prompt {
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(8px);
  pointer-events: auto;
  z-index: 11;
}

.vps-resume-label {
  font-size: 0.9375rem;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.9);
}

.vps-resume-actions {
  display: flex;
  gap: 0.5rem;
}

.vps-resume-btn {
  padding: 0.4rem 1rem;
  border-radius: 0.5rem;
  font-size: 0.875rem;
  font-weight: 500;
  transition:
    background 120ms ease,
    color 120ms ease;
  cursor: pointer;
}

.vps-resume-btn-primary {
  background: var(--ui-primary, #6366f1);
  color: #fff;
}

.vps-resume-btn-primary:hover {
  background: color-mix(in srgb, var(--ui-primary, #6366f1) 85%, #fff);
}

.vps-resume-btn-secondary {
  background: rgba(255, 255, 255, 0.12);
  color: rgba(255, 255, 255, 0.8);
}

.vps-resume-btn-secondary:hover {
  background: rgba(255, 255, 255, 0.2);
  color: #fff;
}

/* Autoplay countdown banner */
.vps-autoplay-banner {
  position: absolute;
  bottom: 0.75rem;
  right: 0.75rem;
  z-index: 20;
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem 0.75rem 0.5rem 0.5rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  pointer-events: auto;
}

.vps-autoplay-ring {
  position: relative;
  width: 2.5rem;
  height: 2.5rem;
  flex-shrink: 0;
}

.vps-autoplay-ring svg {
  transform: rotate(-90deg);
}

.vps-ring-track {
  fill: none;
  stroke: rgba(255, 255, 255, 0.15);
  stroke-width: 3;
}

.vps-ring-progress {
  fill: none;
  stroke: var(--ui-primary, #6366f1);
  stroke-width: 3;
  stroke-linecap: round;
  stroke-dasharray: 100.53;
  transition: stroke-dashoffset 950ms linear;
}

.vps-autoplay-count {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  font-weight: 600;
  color: #fff;
}

.vps-autoplay-label {
  font-size: 0.8125rem;
  color: rgba(255, 255, 255, 0.8);
  white-space: nowrap;
}

.vps-autoplay-cancel,
.vps-autoplay-now {
  padding: 0.25rem 0.625rem;
  border-radius: 0.375rem;
  font-size: 0.8125rem;
  font-weight: 500;
  cursor: pointer;
  transition:
    background 120ms ease,
    color 120ms ease;
  white-space: nowrap;
}

.vps-autoplay-cancel {
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.7);
}
.vps-autoplay-cancel:hover {
  background: rgba(255, 255, 255, 0.18);
  color: #fff;
}

.vps-autoplay-now {
  background: var(--ui-primary, #6366f1);
  color: #fff;
}
.vps-autoplay-now:hover {
  background: color-mix(in srgb, var(--ui-primary, #6366f1) 85%, #fff);
}

/* Track info */
.vps-info {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  min-width: 0;
  margin-top: 0.125rem;
}

.vps-title {
  font-size: 0.875rem;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.vps-dark .vps-title {
  color: rgba(255, 255, 255, 0.9);
}
.vps-light .vps-title {
  color: #1a1a1a;
}

.vps-artist {
  font-size: 0.75rem;
  flex-shrink: 0;
}
.vps-dark .vps-artist {
  color: rgba(255, 255, 255, 0.4);
}
.vps-light .vps-artist {
  color: rgba(0, 0, 0, 0.45);
}

/* Transitions */
.vps-fade-enter-active,
.vps-fade-leave-active {
  transition: opacity 200ms ease;
}
.vps-fade-enter-from,
.vps-fade-leave-to {
  opacity: 0;
}

.vps-slide-up-enter-active,
.vps-slide-up-leave-active {
  transition:
    opacity 200ms ease,
    transform 200ms ease;
}
.vps-slide-up-enter-from,
.vps-slide-up-leave-to {
  opacity: 0;
  transform: translateY(0.5rem);
}
</style>
