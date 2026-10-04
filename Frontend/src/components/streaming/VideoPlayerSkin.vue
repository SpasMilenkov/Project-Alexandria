<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, onMounted, onUnmounted, ref } from "vue";

import { fileApi } from "@/api/file";
import { VIDEO_SHAKA_UI_CONFIG, usePlayerEngine } from "@/composables/usePlayerEngine";
import { useTheme } from "@/composables/useTheme";
import { usePlayerStore } from "@/stores/stream-player";

import PlayerSettings from "./PlayerSettings.vue";

const { railOpen = false } = defineProps<{ railOpen?: boolean }>();

const emit = defineEmits<{
  toggleRail: [];
}>();

const store = usePlayerStore();
const { isDark } = useTheme();
const {
  activeFile,
  hasNext,
  hasPrevious,
  repeatMode,
  context,
  orderBusy,
  isPlaying,
  currentTime,
  duration,
  videoAutoplay,
  autoplayCountdown,
  volume,
} = storeToRefs(store);

const rootRef = ref<HTMLDivElement | null>(null);
const containerRef = ref<HTMLDivElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);

const thumbnailUrl = computed(() => {
  const file = activeFile.value;
  if (!file) return null;
  return fileApi.getThumbnailUrlForVersion(
    file.fileId,
    file.playbackVersionId ?? file.currentVersionId,
  );
});

const {
  isBuffering,
  loadError,
  resumePrompt,
  acceptResumePrompt,
  dismissResumePrompt,
  toggleShakaFullscreen,
} = usePlayerEngine(videoRef, containerRef, {
  getThumbnailUrl: () => thumbnailUrl.value,
  mediaKind: "video",
  shakaUiConfig: {
    ...VIDEO_SHAKA_UI_CONFIG,
    controlPanelElements: [],
    overflowMenuButtons: [],
  },
});

const hasFile = computed(() => activeFile.value?.isVideo ?? false);
const displayTitle = computed(() => activeFile.value?.title ?? activeFile.value?.fileName ?? null);
const displayArtist = computed(() => activeFile.value?.artist ?? null);

const overlayVisible = computed(
  () => !isPlaying.value || resumePrompt.value !== null || autoplayCountdown.value !== null,
);

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
  // Route through Shaka so the button, the F key, and double-tap all enter
  // and exit the same fullscreen element. Shaka owns exit while it owns the
  // element; the root fallback only covers the not-yet-loaded edge.
  if (document.fullscreenElement) {
    if (!toggleShakaFullscreen()) {
      document.exitFullscreen();
    }
    return;
  }
  if (!toggleShakaFullscreen()) {
    rootRef.value?.requestFullscreen();
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

const shuffleHint = computed(() => {
  if (orderBusy.value) return "Starting shuffle…";
  const label = context.value?.label ?? null;
  if (!label) return "Shuffle (applies to the next thing you play)";
  if (context.value?.shuffled) return `Disable shuffle for ${label}`;
  return `Shuffle ${label}`;
});

const countdownDashoffset = computed(() => {
  if (autoplayCountdown.value === null) return RING_CIRCUMFERENCE;
  const fraction = autoplayCountdown.value / AUTOPLAY_SECONDS;
  return RING_CIRCUMFERENCE * (1 - fraction);
});

const onToggleRail = () => emit("toggleRail");

// Compact overflow menu (narrow screens): secondary toggles collapse into a
// pull-up menu so the strip fits 320px viewports.

const showMore = ref(false);
const moreBtnRef = ref<HTMLButtonElement | null>(null);
const moreMenuRef = ref<HTMLDivElement | null>(null);

const closeMore = () => {
  showMore.value = false;
};

// Compact volume popup (narrow screens): the horizontal slider does not fit,
// so the icon alone stays visible and opens a vertical slider on tap.

const showVolPop = ref(false);
const volPopBtnRef = ref<HTMLButtonElement | null>(null);
const volPopRef = ref<HTMLDivElement | null>(null);

const closeVolPop = () => {
  showVolPop.value = false;
};

const toggleVolPop = () => {
  showVolPop.value = !showVolPop.value;
  if (showVolPop.value) closeMore();
};

const toggleMore = () => {
  showMore.value = !showMore.value;
  if (showMore.value) closeVolPop();
};

const REPEAT_STATE_LABELS: Record<string, string> = { off: "Off", all: "All", one: "One" };

const shuffleState = computed(() => {
  if (context.value?.shuffled) return "On";
  return "Off";
});

const repeatState = computed(() => REPEAT_STATE_LABELS[repeatMode.value] ?? repeatMode.value);

const autoplayState = computed(() => {
  if (videoAutoplay.value) return "On";
  return "Off";
});

const railState = computed(() => {
  if (railOpen) return "Shown";
  return "Hidden";
});

const moreShuffle = () => {
  store.toggleShuffle();
  closeMore();
};

const moreRepeat = () => {
  store.toggleLoop();
  closeMore();
};

const moreAutoplay = () => {
  store.toggleVideoAutoplay();
  closeMore();
};

const moreRail = () => {
  onToggleRail();
  closeMore();
};

const onMoreDocClick = (e: MouseEvent) => {
  if (!showMore.value && !showVolPop.value) return;
  const target = e.target as Node;
  const inMore = moreMenuRef.value?.contains(target) || moreBtnRef.value?.contains(target);
  const inVol = volPopRef.value?.contains(target) || volPopBtnRef.value?.contains(target);
  if (!inMore) closeMore();
  if (!inVol) closeVolPop();
};

const onMoreKeydown = (e: KeyboardEvent) => {
  if (e.key !== "Escape") return;
  closeMore();
  closeVolPop();
};

// Lifecycle

onMounted(() => {
  document.addEventListener("fullscreenchange", onFullscreenChange);
  document.addEventListener("click", onMoreDocClick, { capture: true });
  document.addEventListener("keydown", onMoreKeydown);
});

onUnmounted(() => {
  document.removeEventListener("fullscreenchange", onFullscreenChange);
  document.removeEventListener("click", onMoreDocClick, { capture: true });
  document.removeEventListener("keydown", onMoreKeydown);
});

defineExpose({ toggleFullscreen });
</script>

<template>
  <div
    ref="rootRef"
    class="vps-root border border-gray-200/70 dark:border-gray-700/70"
    :class="isDark ? 'vps-dark' : 'vps-light'"
  >
    <!-- Stage -->
    <div class="vps-stage">
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

      <!-- Automatic setup attributes would race with usePlayerEngine's manual attachment. -->
      <div ref="containerRef" class="vps-shaka-container">
        <video ref="videoRef" class="vps-video" playsinline disablepictureinpicture />
      </div>

      <!-- Hover overlay: title gradient + floating dock -->
      <div v-if="hasFile" class="vps-overlay" :class="{ 'vps-overlay-show': overlayVisible }">
        <div class="vps-top">
          <p class="vps-top-title">{{ displayTitle }}</p>
          <p v-if="displayArtist" class="vps-top-meta">{{ displayArtist }}</p>
        </div>
        <div class="vps-dock">
          <div class="vps-seekrow">
            <span class="vps-time">{{ formattedTime(currentTime) }}</span>
            <div class="vps-seek" @click="onProgressClick">
              <div class="vps-seek-track">
                <div class="vps-seek-played" :style="{ width: `${progressPercent}%` }" />
              </div>
              <div class="vps-seek-thumb" :style="{ left: `${progressPercent}%` }" />
            </div>
            <span class="vps-time vps-time-right">{{ formattedTime(duration) }}</span>
          </div>

          <div class="vps-row">
            <div class="vps-group vps-left">
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
                aria-label="Volume"
                @input="onVolumeInput"
              />
            </div>

            <div class="vps-group vps-mid">
              <div class="vps-vol-compact">
                <button
                  ref="volPopBtnRef"
                  type="button"
                  class="vps-btn"
                  title="Volume"
                  aria-label="Volume"
                  :aria-expanded="showVolPop"
                  @click="toggleVolPop"
                >
                  <Icon :icon="volumeIcon" class="w-5 h-5" />
                </button>
                <Transition name="vps-fade">
                  <div v-if="showVolPop" ref="volPopRef" class="vps-vol-pop">
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.02"
                      :value="volume"
                      :style="{ '--vps-vol': `${volume * 100}%` }"
                      class="vps-volume-vertical"
                      title="Volume"
                      aria-label="Volume"
                      aria-orientation="vertical"
                      @input="onVolumeInput"
                    />
                  </div>
                </Transition>
              </div>
              <button
                class="vps-btn vps-collapse"
                :class="context?.shuffled ? 'vps-btn-active' : ''"
                :disabled="orderBusy"
                :title="shuffleHint"
                aria-label="Shuffle"
                @click="store.toggleShuffle()"
              >
                <Icon
                  :icon="orderBusy ? 'mdi:loading' : 'mdi:shuffle-variant'"
                  class="w-5 h-5"
                  :class="orderBusy ? 'animate-spin' : ''"
                />
              </button>
              <button
                class="vps-btn"
                :disabled="!hasPrevious"
                title="Previous"
                aria-label="Previous"
                @click="store.previous()"
              >
                <Icon icon="mdi:skip-previous" class="w-5 h-5" />
              </button>
              <button
                class="vps-btn vps-btn-play"
                :title="isPlaying ? 'Pause' : 'Play'"
                :aria-label="isPlaying ? 'Pause' : 'Play'"
                @click="handlePlayToggle"
              >
                <Icon :icon="isPlaying ? 'mdi:pause' : 'mdi:play'" class="w-6 h-6" />
              </button>
              <button
                class="vps-btn"
                :disabled="!hasNext"
                title="Next"
                aria-label="Next"
                @click="store.next()"
              >
                <Icon icon="mdi:skip-next" class="w-5 h-5" />
              </button>
              <button
                class="vps-btn vps-collapse"
                :class="repeatMode !== 'off' ? 'vps-btn-active' : ''"
                title="Repeat"
                aria-label="Repeat"
                @click="store.toggleLoop()"
              >
                <Icon
                  :icon="repeatMode === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'"
                  class="w-5 h-5"
                />
              </button>
            </div>

            <div class="vps-group vps-right">
              <button
                class="vps-btn vps-collapse"
                :class="videoAutoplay ? 'vps-btn-active' : ''"
                title="Autoplay"
                aria-label="Autoplay"
                :aria-pressed="videoAutoplay"
                @click="store.toggleVideoAutoplay()"
              >
                <Icon icon="mdi:playlist-play" class="w-5 h-5" />
              </button>
              <PlayerSettings />
              <button
                class="vps-btn vps-collapse"
                :class="railOpen ? 'vps-btn-active' : ''"
                title="Up next"
                aria-label="Up next"
                :aria-pressed="railOpen"
                @click="onToggleRail"
              >
                <Icon icon="lucide:list" class="w-5 h-5" />
              </button>
              <button
                class="vps-btn"
                :title="isFullscreen ? 'Exit fullscreen' : 'Fullscreen'"
                :aria-label="isFullscreen ? 'Exit fullscreen' : 'Fullscreen'"
                @click="toggleFullscreen"
              >
                <Icon
                  :icon="isFullscreen ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'"
                  class="w-5 h-5"
                />
              </button>
              <div class="vps-more-wrap">
                <button
                  ref="moreBtnRef"
                  type="button"
                  class="vps-btn"
                  title="More controls"
                  aria-label="More controls"
                  aria-haspopup="menu"
                  :aria-expanded="showMore"
                  @click="toggleMore"
                >
                  <Icon icon="mdi:dots-horizontal" class="w-5 h-5" />
                </button>
                <Transition name="vps-fade">
                  <div
                    v-if="showMore"
                    ref="moreMenuRef"
                    class="vps-more"
                    role="menu"
                    aria-label="More controls"
                  >
                    <button
                      type="button"
                      role="menuitem"
                      class="vps-more-row"
                      :class="context?.shuffled ? 'vps-more-row-active' : ''"
                      :disabled="orderBusy"
                      @click="moreShuffle"
                    >
                      <Icon
                        :icon="orderBusy ? 'mdi:loading' : 'mdi:shuffle-variant'"
                        class="w-5 h-5 shrink-0"
                        :class="orderBusy ? 'animate-spin' : ''"
                      />
                      <span class="flex-1 text-left">Shuffle</span>
                      <span class="vps-more-state">{{ shuffleState }}</span>
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      class="vps-more-row"
                      :class="repeatMode !== 'off' ? 'vps-more-row-active' : ''"
                      @click="moreRepeat"
                    >
                      <Icon
                        :icon="repeatMode === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'"
                        class="w-5 h-5 shrink-0"
                      />
                      <span class="flex-1 text-left">Repeat</span>
                      <span class="vps-more-state">{{ repeatState }}</span>
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      class="vps-more-row"
                      :class="videoAutoplay ? 'vps-more-row-active' : ''"
                      @click="moreAutoplay"
                    >
                      <Icon icon="mdi:playlist-play" class="w-5 h-5 shrink-0" />
                      <span class="flex-1 text-left">Autoplay</span>
                      <span class="vps-more-state">{{ autoplayState }}</span>
                    </button>
                    <button
                      type="button"
                      role="menuitem"
                      class="vps-more-row"
                      :class="railOpen ? 'vps-more-row-active' : ''"
                      @click="moreRail"
                    >
                      <Icon icon="lucide:list" class="w-5 h-5 shrink-0" />
                      <span class="flex-1 text-left">Up next</span>
                      <span class="vps-more-state">{{ railState }}</span>
                    </button>
                  </div>
                </Transition>
              </div>
            </div>
          </div>
        </div>
      </div>

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
    </div>
  </div>
</template>

<style>
@import "shaka-player/dist/controls.css";

/* Root */
.vps-root {
  position: relative;
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: var(--ui-radius);
  overflow: hidden;
  background: #000;
}

.vps-root:fullscreen,
.vps-root:-webkit-full-screen {
  border-radius: 0;
  border: none;
  display: block;
  width: 100%;
  height: 100%;
  background: #000;
}

.vps-root:fullscreen .vps-stage,
.vps-root:-webkit-full-screen .vps-stage {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  max-height: none;
  min-height: 0;
  aspect-ratio: auto;
}

/* Stage */
.vps-stage {
  position: relative;
  width: 100%;
  height: min(56.25vw, 62vh);
  min-height: 20rem;
  background: radial-gradient(
    120% 100% at 50% 0%,
    color-mix(in srgb, var(--ui-primary, #6366f1) 32%, #000000) 0%,
    #000000 78%
  );
  overflow: hidden;
  color: #fff;
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
  backdrop-filter: blur(var(--frost-blur));
  -webkit-backdrop-filter: blur(var(--frost-blur));
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

/* Hover overlay */
.vps-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  opacity: 0;
  transition: opacity 200ms ease-out;
  pointer-events: none;
  z-index: 12;
}

.vps-stage:hover .vps-overlay,
.vps-stage:focus-within .vps-overlay,
.vps-overlay-show {
  opacity: 1;
}

@media (hover: none) {
  .vps-overlay {
    opacity: 1;
  }
}

.vps-top {
  padding: 1rem 1.25rem 2.5rem;
  background: linear-gradient(rgba(0, 0, 0, 0.65), transparent);
}

.vps-top-title {
  font-size: 1rem;
  font-weight: 600;
  color: #fff;
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.vps-top-meta {
  font-size: 0.75rem;
  color: #c4c9d2;
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Floating dock */
.vps-dock {
  pointer-events: auto;
  max-width: 720px;
  width: calc(100% - 2rem);
  margin: 0 auto 1rem;
  padding: 0.625rem 1rem 0.5rem;
  border-radius: 1rem;
  background: rgba(16, 16, 20, 0.84);
  border: 1px solid rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
}

/* Seek row */
.vps-seekrow {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.vps-time {
  font-size: 0.75rem;
  color: #c4c9d2;
  font-variant-numeric: tabular-nums;
  min-width: 2.375rem;
  flex-shrink: 0;
}

.vps-time-right {
  text-align: right;
}

.vps-seek {
  position: relative;
  flex: 1;
  height: 1.25rem;
  display: flex;
  align-items: center;
  cursor: pointer;
  min-width: 0;
}

.vps-seek-track {
  position: relative;
  width: 100%;
  height: 4px;
  border-radius: 2px;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.25);
  transition: height 100ms ease;
}

.vps-seek:hover .vps-seek-track {
  height: 6px;
}

.vps-seek-played {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  background: var(--ui-primary, #6366f1);
  border-radius: 2px;
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

/* Control row: three zones */
.vps-row {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
}

.vps-group {
  display: flex;
  align-items: center;
  gap: 0.125rem;
  min-width: 0;
}

.vps-mid {
  justify-content: center;
}

.vps-right {
  justify-content: flex-end;
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
  color: #c4c9d2;
  transition:
    color 120ms ease,
    background 120ms ease;
}

.vps-btn:hover:not(:disabled) {
  color: #fff;
  background: rgba(255, 255, 255, 0.1);
}

.vps-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.vps-btn-play {
  width: 3rem;
  height: 3rem;
  border-radius: 50%;
  background: var(--ui-primary, #6366f1);
  color: #fff;
  margin: 0 0.375rem;
}

.vps-btn-play:hover:not(:disabled) {
  background: var(--ui-primary, #6366f1);
  color: #fff;
  filter: brightness(1.1);
}

.vps-btn-active {
  color: var(--ui-primary, #6366f1) !important;
}

/* Volume slider */
.vps-volume-slider {
  --vps-vol: 100%;
  width: 80px;
  height: 4px;
  border-radius: 2px;
  cursor: pointer;
  appearance: none;
  -webkit-appearance: none;
  outline: none;
  flex-shrink: 0;
  background: linear-gradient(
    to right,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(255, 255, 255, 0.25) var(--vps-vol)
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

/* Light theme: frosted light strip instead of the dark glass.
   The top title bar intentionally stays dark in both modes. */
.vps-light .vps-dock {
  background: rgba(255, 255, 255, 0.88);
  border-color: rgba(0, 0, 0, 0.08);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}

.vps-light .vps-time {
  color: #4b5563;
}

.vps-light .vps-seek-track {
  background: rgba(0, 0, 0, 0.15);
}

.vps-light .vps-btn {
  color: rgba(0, 0, 0, 0.55);
}

.vps-light .vps-btn:hover:not(:disabled) {
  color: #000;
  background: rgba(0, 0, 0, 0.06);
}

.vps-light .vps-volume-slider {
  background: linear-gradient(
    to right,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(0, 0, 0, 0.15) var(--vps-vol)
  );
}

.vps-light .vps-volume-slider::-webkit-slider-thumb {
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
}

.vps-light .vps-volume-slider::-moz-range-thumb {
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
}

/* Compact volume popup */
.vps-vol-compact {
  display: none;
  position: relative;
  align-items: center;
}

.vps-vol-pop {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  bottom: calc(100% + 10px);
  padding: 12px 8px;
  border-radius: 12px;
  background: rgba(20, 20, 24, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.12);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.4);
  z-index: 30;
}

.vps-light .vps-vol-pop {
  background: rgba(255, 255, 255, 0.95);
  border-color: rgba(0, 0, 0, 0.08);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.15);
}

.vps-volume-vertical {
  --vps-vol: 100%;
  writing-mode: vertical-lr;
  direction: rtl;
  width: 28px;
  height: 120px;
  background: transparent;
  cursor: pointer;
  appearance: none;
  -webkit-appearance: none;
  outline: none;
}

.vps-volume-vertical::-webkit-slider-runnable-track {
  width: 4px;
  border-radius: 2px;
  background: linear-gradient(
    to top,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(255, 255, 255, 0.25) var(--vps-vol)
  );
}

.vps-volume-vertical::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
  cursor: pointer;
}

.vps-volume-vertical::-moz-range-track {
  width: 4px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.25);
}

.vps-volume-vertical::-moz-range-progress {
  width: 4px;
  border-radius: 2px;
  background: var(--ui-primary, #6366f1);
}

.vps-volume-vertical::-moz-range-thumb {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
  cursor: pointer;
  border: none;
}

.vps-light .vps-volume-vertical::-webkit-slider-runnable-track {
  background: linear-gradient(
    to top,
    var(--ui-primary, #6366f1) 0%,
    var(--ui-primary, #6366f1) var(--vps-vol),
    rgba(0, 0, 0, 0.15) var(--vps-vol)
  );
}

.vps-light .vps-volume-vertical::-moz-range-track {
  background: rgba(0, 0, 0, 0.15);
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
  backdrop-filter: blur(var(--frost-blur));
  -webkit-backdrop-filter: blur(var(--frost-blur));
  pointer-events: auto;
  z-index: 14;
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
  bottom: 6rem;
  right: 0.75rem;
  z-index: 13;
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem 0.75rem 0.5rem 0.5rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(var(--frost-blur));
  -webkit-backdrop-filter: blur(var(--frost-blur));
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

/* Compact overflow menu */
.vps-more-wrap {
  display: none;
  position: relative;
  align-items: center;
}

.vps-more {
  position: absolute;
  right: 0;
  bottom: calc(100% + 8px);
  min-width: 220px;
  padding: 6px;
  border-radius: 12px;
  background: rgba(20, 20, 24, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.12);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.4);
  z-index: 30;
}

.vps-more-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #e4e4e7;
  cursor: pointer;
  transition: background-color 120ms ease;
}

.vps-more-row:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.08);
}

.vps-more-row:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.vps-more-row-active {
  color: var(--ui-primary, #6366f1);
}

.vps-more-state {
  font-size: 12px;
  font-weight: 400;
  color: #a1a1aa;
  font-variant-numeric: tabular-nums;
}

.vps-more-row-active .vps-more-state {
  color: var(--ui-primary, #6366f1);
}

.vps-light .vps-more {
  background: rgba(255, 255, 255, 0.95);
  border-color: rgba(0, 0, 0, 0.08);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.15);
}

.vps-light .vps-more-row {
  color: #27272a;
}

.vps-light .vps-more-row:hover:not(:disabled) {
  background: rgba(0, 0, 0, 0.05);
}

.vps-light .vps-more-state {
  color: #71717a;
}

@media (max-width: 760px) {
  .vps-left {
    display: none;
  }
  .vps-vol-compact {
    display: flex;
  }
  .vps-row {
    grid-template-columns: 1fr auto;
  }
  .vps-dock {
    width: calc(100% - 1rem);
    margin-bottom: 0.5rem;
  }
}

@media (max-width: 560px) {
  .vps-collapse {
    display: none;
  }
  .vps-more-wrap {
    display: flex;
  }
}
</style>
