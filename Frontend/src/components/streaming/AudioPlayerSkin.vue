<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, onUnmounted, ref, watch } from "vue";
import { useRouter } from "vue-router";

import { useAppToast } from "@/composables/useAppToast";
import { usePlayerModeTransition } from "@/composables/usePlayerModeTransition";
import { usePlayerStore } from "@/stores/stream-player";

import PlayerArtwork from "./PlayerArtwork.vue";
import PlayerQueue from "./PlayerQueue.vue";
import PlayerSettings from "./PlayerSettings.vue";

const router = useRouter();
const store = usePlayerStore();
const {
  activeFile,
  isAudio,
  snapCorner,
  hasNext,
  hasPrevious,
  repeatMode,
  shuffled,
  shuffleBusy,
  isPlaying,
  currentTime,
  duration,
  volume,
  engineBuffering,
  engineLoadError,
  lyricsOpen,
} = storeToRefs(store);

const trackDetailsTo = computed(() =>
  activeFile.value
    ? { name: "track-details" as const, params: { fileId: activeFile.value.fileId } }
    : null,
);

const trackMenuItems = [
  [
    {
      label: "Track details",
      icon: "mdi:information-outline",
      onSelect: () => {
        store.closeTransientSurfaces();
        if (trackDetailsTo.value) void router.push(trackDetailsTo.value);
      },
    },
  ],
];

const goToTrackDetails = () => {
  store.closeTransientSurfaces();
};

const toast = useAppToast();

watch(engineLoadError, (message) => {
  if (message) toast.error("Audio playback failed", message);
});

const shuffleTitle = computed(() => {
  if (shuffleBusy.value) return "Starting shuffle…";
  return shuffled.value ? "Disable shuffle" : "Shuffle this source";
});

const playPauseIcon = computed(() => {
  if (isPlaying.value) return "mdi:pause";
  return "mdi:play";
});

const playPauseLabel = computed(() => {
  if (isPlaying.value) return "Pause";
  return "Play";
});

const repeatIcon = computed(() => {
  if (repeatMode.value === "one") return "mdi:repeat-once";
  return "mdi:repeat";
});

const shuffleToggleClass = computed(() => {
  if (shuffled.value) return "text-primary";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});

const repeatToggleClass = computed(() => {
  if (repeatMode.value === "all" || repeatMode.value === "one") return "text-primary";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});

const muteLabel = computed(() => {
  if (isMuted.value) return "Unmute";
  return "Mute";
});

const lyricsToggleClass = computed(() => {
  if (lyricsOpen.value) return "text-primary";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});
const cardRef = ref<HTMLDivElement | null>(null);
const layoutRef = ref<HTMLDivElement | null>(null);

const formatTime = (seconds: number) => {
  if (!isFinite(seconds) || seconds < 0) return "0:00";
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, "0")}`;
};

const onSeekInput = (e: Event) => {
  store.seek(Number((e.target as HTMLInputElement).value));
};

const premuteVolume = ref(1);
const isMuted = computed(() => volume.value === 0);

const volumeIcon = computed(() => {
  if (volume.value === 0) return "mdi:volume-off";
  if (volume.value < 0.5) return "mdi:volume-medium";
  return "mdi:volume-high";
});

const toggleMute = () => {
  if (isMuted.value) {
    store.setVolume(premuteVolume.value > 0 ? premuteVolume.value : 1);
  } else {
    premuteVolume.value = volume.value;
    store.setVolume(0);
  }
};

const onVolumeInput = (e: Event) => {
  store.setVolume(Number((e.target as HTMLInputElement).value));
};

const activeFileName = computed(
  () => activeFile.value?.title ?? activeFile.value?.fileName ?? null,
);
const activeArtistName = computed(() => activeFile.value?.artist ?? null);

const isBuffering = engineBuffering;

type PlayerMode = "pip" | "strip";
const playerMode = ref<PlayerMode>("strip");
const isMinimized = computed(() => playerMode.value === "pip");
const isStrip = computed(() => playerMode.value === "strip");

// Drag / snap (pip mode)

const isDragging = ref(false);
const cardPos = ref({ x: 0, y: 0 });
const CARD_W = 320;
const MARGIN = 24;

let dragOffset = { x: 0, y: 0 };

const cardHeight = () => cardRef.value?.offsetHeight ?? 180;
const cardWidth = () => cardRef.value?.offsetWidth ?? CARD_W;

const cornerPos = (c: "tl" | "tr" | "bl" | "br") =>
  ({
    tl: { x: MARGIN, y: MARGIN },
    tr: { x: window.innerWidth - cardWidth() - MARGIN, y: MARGIN },
    bl: { x: MARGIN, y: window.innerHeight - cardHeight() - MARGIN },
    br: {
      x: window.innerWidth - cardWidth() - MARGIN,
      y: window.innerHeight - cardHeight() - MARGIN,
    },
  })[c];

const nearestCorner = (): "tl" | "tr" | "bl" | "br" => {
  const cx = cardPos.value.x + cardWidth() / 2;
  const cy = cardPos.value.y + cardHeight() / 2;
  const right = cx > window.innerWidth / 2;
  const bottom = cy > window.innerHeight / 2;
  if (right && bottom) return "br";
  if (right) return "tr";
  if (bottom) return "bl";
  return "tl";
};

const snapTo = (c: "tl" | "tr" | "bl" | "br") => {
  store.setSnapCorner(c);
  cardPos.value = cornerPos(c);
};

// Mouse drag

const onMouseDown = (e: MouseEvent) => {
  if (!isMinimized.value || isSwitching.value) return;
  e.stopPropagation();
  const rect = cardRef.value!.getBoundingClientRect();
  dragOffset = { x: e.clientX - rect.left, y: e.clientY - rect.top };
  isDragging.value = true;
  window.addEventListener("mousemove", onMouseMove);
  window.addEventListener("mouseup", onMouseUp);
};

const onMouseMove = (e: MouseEvent) => {
  if (!isDragging.value) return;
  e.preventDefault();
  const maxX = window.innerWidth - CARD_W;
  const maxY = window.innerHeight - cardHeight();
  cardPos.value = {
    x: Math.max(0, Math.min(maxX, e.clientX - dragOffset.x)),
    y: Math.max(0, Math.min(maxY, e.clientY - dragOffset.y)),
  };
};

const onMouseUp = () => {
  if (!isDragging.value) return;
  isDragging.value = false;
  snapTo(nearestCorner());
  window.removeEventListener("mousemove", onMouseMove);
  window.removeEventListener("mouseup", onMouseUp);
};

// Touch drag (mobile)

const onTouchStart = (e: TouchEvent) => {
  if (!isMinimized.value || isSwitching.value) return;
  e.stopPropagation();
  const touch = e.touches[0];
  const rect = cardRef.value!.getBoundingClientRect();
  dragOffset = { x: touch.clientX - rect.left, y: touch.clientY - rect.top };
  isDragging.value = true;
  window.addEventListener("touchmove", onTouchMove, { passive: false });
  window.addEventListener("touchend", onTouchEnd);
};

const onTouchMove = (e: TouchEvent) => {
  if (!isDragging.value) return;
  e.preventDefault();
  const touch = e.touches[0];
  const maxX = window.innerWidth - CARD_W;
  const maxY = window.innerHeight - cardHeight();
  cardPos.value = {
    x: Math.max(0, Math.min(maxX, touch.clientX - dragOffset.x)),
    y: Math.max(0, Math.min(maxY, touch.clientY - dragOffset.y)),
  };
};

const onTouchEnd = () => {
  if (!isDragging.value) return;
  isDragging.value = false;
  snapTo(nearestCorner());
  window.removeEventListener("touchmove", onTouchMove);
  window.removeEventListener("touchend", onTouchEnd);
};

const cardStyle = computed(() => {
  if (playerMode.value !== "pip") return {};
  return {
    position: "fixed" as const,
    top: `${cardPos.value.y}px`,
    left: `${cardPos.value.x}px`,
    width: `${CARD_W}px`,
    transition:
      isDragging.value || isSwitching.value
        ? "none"
        : "top 420ms cubic-bezier(0.34,1.4,0.64,1), left 420ms cubic-bezier(0.34,1.4,0.64,1)",
  };
});

const modeIcon = computed(() => {
  if (playerMode.value === "strip") return "mdi:picture-in-picture-bottom-right";
  return "mdi:dock-bottom";
});

const cardVariantClass = computed(() => {
  if (isStrip.value) return "w-full rounded-t-xl";
  return "rounded-2xl";
});

const dragCursorClass = computed(() => {
  if (isDragging.value) return "cursor-grabbing";
  return "cursor-grab";
});

const playerAreaClass = computed(() => {
  if (isStrip.value) return "h-[88px]";
  return "";
});

const { isSwitching, toggle: cycleMode } = usePlayerModeTransition({
  card: cardRef,
  layout: layoutRef,
  mode: playerMode,
  positionFloating: () => {
    if (!isDragging.value) snapTo(snapCorner.value);
  },
  beforeChange: () => store.closeTransientSurfaces(),
});

watch(playerMode, (m) => store.setPlayerMode(m), { immediate: true });

onUnmounted(() => {
  window.removeEventListener("mousemove", onMouseMove);
  window.removeEventListener("mouseup", onMouseUp);
  window.removeEventListener("touchmove", onTouchMove);
  window.removeEventListener("touchend", onTouchEnd);
});
</script>

<template>
  <div
    ref="layoutRef"
    class="relative isolate hidden md:block w-full"
    :class="{
      'h-0': playerMode === 'pip',
      'z-40': isMinimized || isSwitching,
      'z-30': isStrip && !isSwitching,
    }"
  >
    <div
      ref="cardRef"
      class="@container player-card frosted-glass glass-surface border border-black/[0.08] dark:border-white/10 overflow-hidden"
      :class="[
        cardVariantClass,
        {
          minimized: isMinimized,
          strip: isStrip,
          'is-dragging': isDragging,
        },
      ]"
      :style="cardStyle"
    >
      <!-- Floating header: drag handle, identity, dock and close -->
      <div
        v-show="isMinimized"
        class="flex items-center justify-between px-3 py-2 border-b border-gray-200/70 dark:border-white/[0.07] select-none"
      >
        <div class="flex items-center gap-1 min-w-0">
          <span
            aria-hidden="true"
            class="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 touch-none select-none"
            :class="dragCursorClass"
            @mousedown="onMouseDown"
            @touchstart.prevent="onTouchStart"
          >
            <Icon icon="mdi:grip-vertical" class="w-5 h-5" />
          </span>
          <p
            class="font-semibold truncate text-sm text-gray-800 dark:text-white/90"
            :title="activeFileName ?? 'Unknown'"
          >
            {{ activeFileName ?? "Unknown" }}
          </p>
        </div>

        <div class="flex items-center gap-1 shrink-0">
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
            title="Dock to strip"
            aria-label="Dock to strip"
            data-player-mode-toggle="pip"
            @click="cycleMode"
          >
            <Icon :icon="modeIcon" class="w-5 h-5" />
          </button>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
            title="Close player"
            aria-label="Close player"
            @click="store.closePlayer()"
          >
            <Icon icon="mdi:close" class="w-5 h-5" />
          </button>
        </div>
      </div>

      <!--
        Player area.
        Pip: artwork with overlaid transport, seek row, and utility row.
        Strip: the grid zone below spans the full row width.
      -->
      <div class="relative" :class="playerAreaClass">
        <!-- Floating body -->
        <div v-if="!isStrip" class="flex flex-col">
          <div data-player-artwork-frame class="relative aspect-video overflow-hidden bg-black">
            <PlayerArtwork
              :file="activeFile"
              :alt="activeFileName ?? ''"
              icon-class="w-12 h-12 text-gray-400 dark:text-white/30"
            />
            <div
              class="absolute inset-x-0 bottom-0 h-24 bg-gradient-to-t from-black/60 to-transparent pointer-events-none"
              aria-hidden="true"
            />
            <div class="absolute inset-x-0 bottom-0 flex items-center justify-center gap-2 pb-2">
              <button
                class="w-10 h-10 rounded-full flex items-center justify-center text-white/80 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasPrevious"
                title="Previous"
                aria-label="Previous"
                @click="store.previous()"
              >
                <Icon icon="mdi:skip-previous" class="w-6 h-6" />
              </button>
              <button
                class="w-12 h-12 rounded-full flex items-center justify-center bg-primary text-white hover:opacity-90 disabled:opacity-30 disabled:cursor-not-allowed transition-opacity"
                :disabled="!activeFile"
                :title="playPauseLabel"
                :aria-label="playPauseLabel"
                data-player-transport-focus
                @click="store.togglePlay()"
              >
                <Icon v-if="isBuffering" icon="mdi:loading" class="w-6 h-6 animate-spin" />
                <Icon v-else :icon="playPauseIcon" class="w-6 h-6" />
              </button>
              <button
                class="w-10 h-10 rounded-full flex items-center justify-center text-white/80 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasNext"
                title="Next"
                aria-label="Next"
                @click="store.next()"
              >
                <Icon icon="mdi:skip-next" class="w-6 h-6" />
              </button>
            </div>
          </div>
          <div class="flex items-center gap-2 px-3 pt-2">
            <span class="seek-time flex-shrink-0 text-gray-500 dark:text-gray-500">{{
              formatTime(currentTime)
            }}</span>
            <input
              type="range"
              class="transport-seek flex-1 min-w-0 accent-primary"
              :max="duration || 0"
              :step="0.5"
              :value="currentTime"
              aria-label="Seek"
              @input="onSeekInput"
            />
            <span class="seek-time flex-shrink-0 text-gray-500 dark:text-gray-500">{{
              formatTime(duration)
            }}</span>
          </div>
          <div class="flex items-center justify-center gap-0.5 px-3 pb-2">
            <button
              class="w-9 h-9 rounded-lg flex items-center justify-center transition-colors"
              :class="shuffleToggleClass"
              :disabled="shuffleBusy"
              :title="shuffleTitle"
              :aria-label="shuffleTitle"
              @click="store.toggleShuffle()"
            >
              <Icon v-if="shuffleBusy" icon="mdi:loading" class="w-4 h-4 animate-spin" />
              <Icon v-else icon="mdi:shuffle-variant" class="w-4 h-4" />
            </button>
            <button
              class="w-9 h-9 rounded-lg flex items-center justify-center transition-colors"
              :class="repeatToggleClass"
              title="Repeat"
              aria-label="Repeat"
              @click="store.toggleLoop()"
            >
              <Icon :icon="repeatIcon" class="w-4 h-4" />
            </button>
            <PlayerQueue />
            <PlayerSettings />
          </div>
        </div>

        <!-- Strip zones: artwork and details, centered transport and seek, utilities -->
        <div
          v-if="isStrip"
          id="audio-strip"
          class="h-[88px] grid grid-cols-[minmax(0,1fr)_minmax(280px,420px)_minmax(0,1fr)] items-center gap-4 px-4"
        >
          <div class="flex items-center gap-4 min-w-0">
            <RouterLink
              v-if="trackDetailsTo"
              :to="trackDetailsTo"
              data-player-artwork-frame
              class="w-14 h-14 rounded-lg overflow-hidden bg-gray-100 dark:bg-gray-800 flex-shrink-0"
              aria-label="Open track details"
              @click="goToTrackDetails"
            >
              <PlayerArtwork
                :file="activeFile"
                :alt="activeFileName ?? ''"
                icon-class="w-6 h-6 text-gray-400 dark:text-white/30"
              />
            </RouterLink>
            <div
              v-else
              data-player-artwork-frame
              class="w-14 h-14 rounded-lg overflow-hidden bg-gray-100 dark:bg-gray-800 flex-shrink-0"
            >
              <PlayerArtwork :file="null" />
            </div>
            <div class="flex-1 min-w-0 max-w-60">
              <RouterLink
                v-if="trackDetailsTo"
                :to="trackDetailsTo"
                class="block text-sm font-semibold truncate text-gray-900 dark:text-gray-100 m-0 hover:text-primary transition-colors"
                :title="activeFileName ?? 'Unknown'"
                @click="goToTrackDetails"
              >
                {{ activeFileName ?? "Unknown" }}
              </RouterLink>
              <p v-else class="text-sm font-semibold truncate text-gray-900 dark:text-gray-100 m-0">
                Unknown
              </p>
              <p
                v-if="activeArtistName"
                class="text-xs truncate text-gray-600 dark:text-gray-400 m-0"
                :title="activeArtistName"
              >
                {{ activeArtistName }}
              </p>
            </div>
          </div>

          <div class="flex flex-col justify-center gap-1 min-w-0">
            <div class="flex items-center justify-center gap-2">
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
                :class="shuffleToggleClass"
                :disabled="shuffleBusy"
                :title="shuffleTitle"
                :aria-label="shuffleTitle"
                @click="store.toggleShuffle()"
              >
                <Icon v-if="shuffleBusy" icon="mdi:loading" class="w-5 h-5 animate-spin" />
                <Icon v-else icon="mdi:shuffle-variant" class="w-5 h-5" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-700 dark:text-white/85 hover:text-gray-950 dark:hover:text-white disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasPrevious"
                title="Previous"
                aria-label="Previous"
                @click="store.previous()"
              >
                <Icon icon="mdi:skip-previous" class="w-6 h-6" />
              </button>
              <button
                class="w-12 h-12 rounded-full flex-shrink-0 flex items-center justify-center bg-primary text-white hover:opacity-90 disabled:opacity-30 disabled:cursor-not-allowed transition-opacity"
                :disabled="!activeFile"
                :title="playPauseLabel"
                :aria-label="playPauseLabel"
                data-player-transport-focus
                @click="store.togglePlay()"
              >
                <Icon v-if="isBuffering" icon="mdi:loading" class="w-6 h-6 animate-spin" />
                <Icon v-else :icon="playPauseIcon" class="w-6 h-6" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-700 dark:text-white/85 hover:text-gray-950 dark:hover:text-white disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasNext"
                title="Next"
                aria-label="Next"
                @click="store.next()"
              >
                <Icon icon="mdi:skip-next" class="w-6 h-6" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
                :class="repeatToggleClass"
                title="Repeat"
                aria-label="Repeat"
                @click="store.toggleLoop()"
              >
                <Icon :icon="repeatIcon" class="w-5 h-5" />
              </button>
            </div>
            <div class="flex items-center gap-2">
              <span class="seek-time flex-shrink-0 text-gray-500 dark:text-gray-500">{{
                formatTime(currentTime)
              }}</span>
              <input
                type="range"
                class="transport-seek flex-1 min-w-0 accent-primary"
                :max="duration || 0"
                :step="0.5"
                :value="currentTime"
                aria-label="Seek"
                @input="onSeekInput"
              />
              <span class="seek-time flex-shrink-0 text-gray-500 dark:text-gray-500">{{
                formatTime(duration)
              }}</span>
            </div>
          </div>

          <div class="flex items-center justify-end gap-0.5 min-w-0">
            <div class="hidden @min-[800px]:flex items-center flex-shrink-0">
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
                :title="muteLabel"
                :aria-label="muteLabel"
                @click="toggleMute"
              >
                <Icon :icon="volumeIcon" class="w-5 h-5" />
              </button>
              <input
                type="range"
                class="volume-slider w-20 flex-shrink-0 accent-primary"
                :min="0"
                :max="1"
                :step="0.02"
                :value="volume"
                aria-label="Volume"
                @input="onVolumeInput"
              />
            </div>
            <PlayerQueue />
            <PlayerSettings />
            <button
              class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
              :class="lyricsToggleClass"
              title="Lyrics"
              aria-label="Lyrics"
              :aria-pressed="lyricsOpen"
              @click="store.toggleLyrics()"
            >
              <Icon icon="mdi:script-text-outline" class="w-5 h-5" />
            </button>
            <UDropdownMenu :items="trackMenuItems" :content="{ align: 'end' }">
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
                title="More options"
                aria-label="More options"
              >
                <Icon icon="mdi:dots-horizontal" class="w-5 h-5" />
              </button>
            </UDropdownMenu>
            <button
              class="hidden @min-[1100px]:flex w-10 h-10 rounded-lg items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 transition-colors"
              title="Mini player"
              aria-label="Mini player"
              data-player-mode-toggle="strip"
              @click="cycleMode"
            >
              <Icon :icon="modeIcon" class="w-5 h-5" />
            </button>
            <button
              class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
              title="Close player"
              aria-label="Close player"
              @click="store.closePlayer()"
            >
              <Icon icon="mdi:close" class="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
.player-card.player-motion-live,
.player-card.player-motion-snapshot {
  background: transparent !important;
  border-color: transparent !important;
  backdrop-filter: none !important;
  -webkit-backdrop-filter: none !important;
}

.player-card.player-motion-live {
  position: relative;
  z-index: 2;
}

.player-motion-live [data-player-artwork] {
  visibility: hidden;
}

.player-motion-live [data-player-artwork-frame],
.player-motion-snapshot [data-player-artwork-frame] {
  background: transparent !important;
}

.player-motion-snapshot,
.player-motion-snapshot * {
  animation: none !important;
  transition: none !important;
}

.player-motion-artwork {
  object-fit: cover;
  overflow: hidden;
}

.player-card {
  --seek-base: rgba(255, 255, 255, 0.18);
  --seek-buffered: rgba(255, 255, 255, 0.35);
  --seek-played: var(--ui-primary);
}

.player-card .seek-time {
  font-size: 0.6875rem;
  font-variant-numeric: tabular-nums;
}

.player-card .transport-seek,
.player-card .volume-slider {
  cursor: pointer;
}

.player-card .shaka-video-container {
  background: transparent !important;
  isolation: isolate;
  pointer-events: auto !important;
}
.player-card .shaka-controls-container {
  z-index: 30 !important;
  pointer-events: auto !important;
}
.player-card .shaka-bottom-controls {
  z-index: 31 !important;
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.5) 0%,
    rgba(0, 0, 0, 0.18) 55%,
    transparent 100%
  );
  padding: 0 16px 12px;
}
.player-card .shaka-asset-title,
.player-card .shaka-content-title {
  display: none !important;
}
.player-card .shaka-ui-icon {
  width: 2rem;
  height: 2rem;
}
.player-card .shaka-controls-button-panel {
  align-items: center;
  gap: 4px;
}
.player-card .shaka-play-button {
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.14);
  border: 1px solid rgba(255, 255, 255, 0.18);
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(var(--frost-blur));
  -webkit-backdrop-filter: blur(var(--frost-blur));
  transition:
    background 160ms ease,
    transform 160ms ease;
}
.player-card .shaka-play-button:hover {
  background: rgba(var(--ui-primary), 0.7);
  transform: scale(1.04);
}
.player-card .shaka-volume-bar-container {
  margin-left: 0;
}
.player-card .shaka-overflow-menu.shaka-overflow-menu-shown,
.player-card .shaka-settings-menu.shaka-displayed {
  pointer-events: auto;
}
.player-card .shaka-scrim-container {
  background: transparent !important;
}

/* Strip-specific overrides */
.player-card.strip .shaka-bottom-controls {
  background: none !important;
  padding: 2px 6px 4px !important;
  transition: transform 240ms cubic-bezier(0.4, 0, 0.2, 1);
  will-change: transform;
}
.player-card.strip .audio-shaka-container {
  overflow: hidden;
}
.player-card.strip .shaka-tooltip {
  overflow: visible;
  bottom: calc(100% + 6px) !important;
  top: auto !important;
}
.player-card.strip .shaka-controls-container,
.player-card.strip .shaka-controls-container[shown="false"] {
  opacity: 1 !important;
}
.player-card.strip .shaka-play-button {
  width: 34px !important;
  height: 34px !important;
  padding: 0 !important;
  background: rgba(128, 128, 128, 0.12);
  border-color: rgba(128, 128, 128, 0.2);
  box-shadow: none;
}
.player-card.strip .shaka-controls-button-panel {
  align-items: center;
  gap: 2px;
}
.player-card.strip .shaka-controls-button-panel .material-icons-round,
.player-card.strip .shaka-play-button .material-icons-round {
  font-size: 18px !important;
}
.player-card.strip .shaka-volume-bar-container {
  max-width: 60px !important;
}
.player-card.strip .shaka-ui-icon {
  width: 1.25rem;
  height: 1.25rem;
}
.player-card.minimized .shaka-volume-bar-container {
  display: none;
}

/*
  Strip buffering indicator.
  Audio-only, so the big centered Shaka spinner overlay is out of place —
  swap it for a small ring around the play button instead.
*/
.player-card.strip .shaka-spinner-container {
  display: none !important;
}
.player-card.strip .shaka-play-button {
  position: relative;
  overflow: visible;
}
.player-card.strip.is-buffering .shaka-play-button::after {
  content: "";
  position: absolute;
  inset: -3px;
  border-radius: 999px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  opacity: 0.7;
  animation: shaka-btn-spin 0.7s linear infinite;
  pointer-events: none;
}
.player-card.strip.is-buffering .shaka-play-button .material-icons-round {
  opacity: 0.5;
}

@keyframes shaka-btn-spin {
  to {
    transform: rotate(360deg);
  }
}

/* Compact strip */
.player-card.strip.compact-strip .shaka-controls-button-panel {
  opacity: 0;
  pointer-events: none;
  transition: opacity 180ms ease;
}
.player-card.strip.compact-strip:hover .shaka-controls-button-panel {
  opacity: 1;
  pointer-events: auto;
}
.player-card .shaka-controls-container[shown="true"] .shaka-big-buttons-container button {
  opacity: 0;
}

/* Light theme */
.player-card.theme-light.strip .shaka-current-time,
.player-card.theme-light.strip .shaka-duration {
  color: #1a1a1a !important;
}
.player-card.theme-light.strip .material-icons-round {
  color: #2a2a2a !important;
}
.player-card.theme-light.strip .shaka-play-button {
  background: rgba(0, 0, 0, 0.08) !important;
  border-color: rgba(0, 0, 0, 0.18) !important;
}
.player-card.theme-light.strip .shaka-controls-button-panel > *,
.player-card.theme-light.strip .shaka-controls-top-button-panel > * {
  color: #2a2a2a;
}
.player-card.theme-light.strip
  .shaka-seek-bar-container
  .shaka-range-element::-webkit-slider-runnable-track {
  background: rgba(0, 0, 0, 0.15);
}
.player-card.theme-light.strip
  .shaka-seek-bar-container
  .shaka-range-element::-webkit-slider-thumb {
  background: #1a1a1a;
}
.player-card.theme-light.strip
  .shaka-volume-bar-container
  .shaka-range-element::-webkit-slider-runnable-track {
  background: rgba(0, 0, 0, 0.15);
  height: 3px;
  border-radius: 2px;
}
.player-card.theme-light.strip
  .shaka-volume-bar-container
  .shaka-range-element::-webkit-slider-thumb {
  background: #1a1a1a;
}
.player-card.theme-light.strip .shaka-volume-bar-container .shaka-range-element::-moz-range-thumb {
  background: #1a1a1a;
  border: none;
}
.player-card.theme-light.strip .shaka-volume-bar-container .shaka-range-element::-moz-range-track {
  background: rgba(0, 0, 0, 0.15);
  height: 3px;
  border-radius: 2px;
}

@media (prefers-reduced-motion: reduce) {
  .player-card,
  .player-card * {
    transition: none !important;
    animation: none !important;
  }
}

@media (max-width: 756px) {
  .player-card.strip:not(.compact-strip) > div.relative {
    flex-wrap: wrap;
    height: auto !important;
  }

  .player-card.strip:not(.compact-strip) > div.relative > div:first-child {
    width: 100% !important;
    max-width: 100% !important;
    flex-shrink: 0;
    border-right: none !important;
    border-bottom: 1px solid rgba(0, 0, 0, 0.06);
    height: 3rem;
  }
  .dark .player-card.strip:not(.compact-strip) > div.relative > div:first-child {
    border-bottom-color: rgba(255, 255, 255, 0.07);
  }

  .player-card.strip:not(.compact-strip) > div.relative > button {
    height: 68px;
    display: flex;
    align-items: center;
  }
  .player-card.strip:not(.compact-strip) > div.relative > div.audio-shaka-container {
    height: 72px !important;
    min-height: 72px;
    display: flex;
    align-items: center;
  }

  .player-card.strip:not(.compact-strip) .audio-shaka-container {
    overflow: visible !important;
  }

  .player-card.strip:not(.compact-strip) > div.relative > div:last-child {
    width: 100% !important;
    justify-content: center;
    border-left: none !important;
    border-top: 1px solid rgba(0, 0, 0, 0.06);
    padding: 4px 0 !important;
    height: 40px;
  }
  .dark .player-card.strip:not(.compact-strip) > div.relative > div:last-child {
    border-top-color: rgba(255, 255, 255, 0.07);
  }

  .player-card.minimized {
    width: 260px !important;
  }
  .player-card.minimized .aspect-video {
    aspect-ratio: 1 / 1 !important;
  }
  .shaka-controls-container[shown="true"] .shaka-big-buttons-container button {
    opacity: 0;
  }
}

.player-card.strip.compact-strip {
  height: 40px;
  overflow: hidden;
  transition:
    height 240ms cubic-bezier(0.4, 0, 0.2, 1),
    overflow 0ms 240ms;
}

.player-card.strip.compact-strip:hover {
  height: 108px;
  overflow: visible;
  transition: height 240ms cubic-bezier(0.4, 0, 0.2, 1);
}

.player-card.strip.compact-strip:hover > div.relative {
  flex-wrap: wrap;
  height: auto !important;
}

.player-card.strip.compact-strip > div.relative > div:first-child {
  width: auto !important;
  max-width: 35%;
  border-right: none !important;
}
.player-card.strip.compact-strip:hover > div.relative > div:first-child {
  width: 100% !important;
  max-width: 100% !important;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  height: 50px;
}
.dark .player-card.strip.compact-strip:hover > div.relative > div:first-child {
  border-bottom-color: rgba(255, 255, 255, 0.07);
}

.player-card.strip.compact-strip > div.relative > div:first-child > div.w-9 {
  width: 0 !important;
  overflow: hidden;
  opacity: 0;
  margin: 0;
  transition:
    width 240ms ease,
    opacity 180ms ease;
}
.player-card.strip.compact-strip:hover > div.relative > div:first-child > div.w-9 {
  width: 2.25rem !important;
  opacity: 1;
}

.player-card.strip.compact-strip > div.relative > div:first-child p {
  max-width: 100px !important;
}

.player-card.strip.compact-strip > div.relative > button {
  width: 0 !important;
  opacity: 0;
  overflow: hidden;
  pointer-events: none;
  transition:
    width 240ms ease,
    opacity 180ms ease;
}
.player-card.strip.compact-strip:hover > div.relative > button {
  width: 2.25rem !important;
  height: 68px !important;
  opacity: 1;
  pointer-events: auto;
}
.player-card.strip.compact-strip:hover > div.relative > div.audio-shaka-container {
  height: 68px !important;
  min-height: 68px;
}

.player-card.strip.compact-strip:hover .audio-shaka-container {
  overflow: visible !important;
}

.player-card.strip.compact-strip > div.relative > div:last-child {
  display: none !important;
}

.player-card.strip.compact-strip .shaka-controls-button-panel {
  opacity: 0;
  pointer-events: none;
  transition: opacity 180ms ease;
}
.player-card.strip.compact-strip:hover .shaka-controls-button-panel {
  opacity: 1;
  pointer-events: auto;
}

.player-card.theme-light.strip .shaka-controls-button-panel > *,
.player-card.theme-light.strip .shaka-controls-top-button-panel > * {
  color: #2a2a2a;
}

.player-card.theme-light.strip
  .shaka-volume-bar-container
  .shaka-range-element::-webkit-slider-thumb {
  background: #1a1a1a;
}

.player-card.theme-light.strip .shaka-volume-bar-container .shaka-range-element::-moz-range-thumb {
  background: #1a1a1a;
  border: none;
}
.player-card.theme-light.strip
  .shaka-volume-bar-container
  .shaka-range-element::-webkit-slider-runnable-track {
  background: rgba(0, 0, 0, 0.15);
  height: 3px;
  border-radius: 2px;
}

.player-card.theme-light.strip .shaka-volume-bar-container .shaka-range-element::-moz-range-track {
  background: rgba(0, 0, 0, 0.15);
  height: 3px;
  border-radius: 2px;
}
</style>
