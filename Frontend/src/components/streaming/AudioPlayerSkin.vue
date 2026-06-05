<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { useDark } from "@vueuse/core";
import { storeToRefs } from "pinia";
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute } from "vue-router";

import { usePlayerEngine } from "@/composables/usePlayerEngine";
import { getPreview } from "@/queries/files";
import { usePlayerStore } from "@/stores/stream-player";

import PlayerQueue from "./PlayerQueue.vue";
import PlayerSettings from "./PlayerSettings.vue";

const route = useRoute();
const store = usePlayerStore();
const { activeFile, isAudio, snapCorner, hasNext, hasPrevious, repeatMode, shuffled } =
  storeToRefs(store);

const isDark = useDark();

const cardRef = ref<HTMLDivElement | null>(null);
const containerRef = ref<HTMLDivElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);

const { data: preview } = useQuery(() => getPreview(activeFile.value?.fileId ?? ""));
const audioBg = computed(() => preview.value?.thumbnailUrl ?? null);
const activeFileName = computed(() => activeFile.value?.fileName ?? null);

const { isBuffering, loadError } = usePlayerEngine(videoRef, containerRef, {
  getThumbnailUrl: () => audioBg.value,
});

void isBuffering;
void loadError;

type PlayerMode = "pip" | "strip";
const playerMode = ref<PlayerMode>("strip");
const isMinimized = computed(() => playerMode.value === "pip");
const isStrip = computed(() => playerMode.value === "strip");

const isCompactStrip = computed(
  () => isStrip.value && route.path !== "/streaming/music" && !route.path.includes("/streaming"),
);

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
  return right && bottom ? "br" : right ? "tr" : bottom ? "bl" : "tl";
};

const snapTo = (c: "tl" | "tr" | "bl" | "br") => {
  store.setSnapCorner(c);
  cardPos.value = cornerPos(c);
};

// Mouse drag

const onMouseDown = (e: MouseEvent) => {
  if (!isMinimized.value) return;
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
  if (!isMinimized.value) return;
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

const onResize = () => {
  if (isMinimized.value && !isDragging.value) snapTo(snapCorner.value);
};

const cardStyle = computed(() => {
  if (playerMode.value !== "pip") return {};
  return {
    position: "fixed" as const,
    top: `${cardPos.value.y}px`,
    left: `${cardPos.value.x}px`,
    width: `${CARD_W}px`,
    zIndex: 9999,
    transition: isDragging.value
      ? "none"
      : "top 420ms cubic-bezier(0.34,1.4,0.64,1), left 420ms cubic-bezier(0.34,1.4,0.64,1)",
  };
});

// Mode toggle: strip <-> pip

const modeIcon = computed(() =>
  playerMode.value === "strip" ? "mdi:picture-in-picture-bottom-right" : "mdi:dock-bottom",
);

const cycleMode = async () => {
  if (playerMode.value === "strip") {
    playerMode.value = "pip";
    await nextTick();
    requestAnimationFrame(() => snapTo("br"));
  } else {
    playerMode.value = "strip";
  }
};

watch(playerMode, (m) => store.setPlayerMode(m), { immediate: true });

onMounted(async () => {
  await nextTick();
  requestAnimationFrame(() => snapTo(snapCorner.value));
  window.addEventListener("resize", onResize);
});

onUnmounted(() => {
  window.removeEventListener("resize", onResize);
  window.removeEventListener("mousemove", onMouseMove);
  window.removeEventListener("mouseup", onMouseUp);
  window.removeEventListener("touchmove", onTouchMove);
  window.removeEventListener("touchend", onTouchEnd);
});
</script>

<template>
  <div
    :class="{
      'h-0': playerMode === 'pip',
      ' w-full z-[9999]': playerMode === 'strip',
    }"
  >
    <div
      ref="cardRef"
      class="player-card bg-white/75 dark:bg-white/[0.06] backdrop-blur-sm border border-black/[0.08] dark:border-white/10 overflow-hidden"
      :class="[
        isStrip ? 'w-full rounded-t-xl' : 'rounded-2xl',
        {
          minimized: isMinimized,
          strip: isStrip,
          'is-dragging': isDragging,
          'compact-strip': isCompactStrip,
          'theme-dark': isDark,
          'theme-light': !isDark,
        },
      ]"
      :style="cardStyle"
    >
      <!--
        PiP header: only the essentials to keep it uncluttered.
        Shuffle / loop / settings live in the expanded strip; in pip the
        user just needs track identity, skip controls, queue, mode & close.
      -->
      <div
        v-show="isMinimized"
        class="flex items-center justify-between px-3 py-2 border-b border-gray-200/70 dark:border-white/[0.07] select-none"
        :class="isDragging ? 'cursor-grabbing' : 'cursor-grab'"
        @mousedown="onMouseDown"
        @touchstart.prevent="onTouchStart"
      >
        <div class="min-w-0">
          <p class="font-semibold truncate text-sm text-gray-800 dark:text-white/90">
            {{ activeFileName ?? "Unknown" }}
          </p>
        </div>

        <div class="flex items-center gap-1 shrink-0" @mousedown.stop @touchstart.stop>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            :disabled="!hasPrevious"
            @click="store.previous()"
          >
            <Icon icon="mdi:skip-previous" class="w-5 h-5" />
          </button>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            :disabled="!hasNext"
            @click="store.next()"
          >
            <Icon icon="mdi:skip-next" class="w-5 h-5" />
          </button>
          <PlayerQueue />
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
            @click="cycleMode"
          >
            <Icon :icon="modeIcon" class="w-5 h-5" />
          </button>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
            @click="store.clearActiveFile()"
          >
            <Icon icon="mdi:close" class="w-5 h-5" />
          </button>
        </div>
      </div>

      <!--
        Player area.
        Pip: bg-black, aspect-video (shows artwork or video element).
        Strip: flex row, fixed height, no overflow-hidden so Shaka tooltips survive.
      -->
      <div
        class="relative"
        :class="
          isStrip
            ? 'flex items-stretch h-[102px] shaka-audio-only'
            : 'bg-black rounded-b-2xl aspect-video overflow-hidden'
        "
      >
        <!-- Audio artwork overlay (pip only) -->
        <Transition name="overlay-fade">
          <div
            v-if="isAudio && audioBg && !isStrip"
            class="absolute inset-0 overflow-hidden pointer-events-none"
            aria-hidden="true"
          >
            <img
              :src="audioBg"
              alt=""
              class="absolute inset-0 w-full h-full object-cover scale-110 blur-sm opacity-40"
            />
            <img :src="audioBg" alt="" class="absolute inset-0 w-full h-full object-cover" />
            <div class="absolute inset-0 bg-black/25" />
          </div>
        </Transition>

        <!-- Strip: cover art + track name -->
        <div
          v-show="isStrip"
          class="flex gap-2.5 px-3 w-52 flex-shrink-0 border-r border-black/[0.06] dark:border-white/[0.07]"
          :class="isCompactStrip ? 'items-start py-2' : 'items-center py-0'"
        >
          <div
            class="w-9 h-9 rounded-md overflow-hidden bg-gray-100 dark:bg-gray-800 flex-shrink-0"
          >
            <img
              v-if="audioBg"
              :src="audioBg"
              :alt="activeFileName ?? ''"
              class="w-full h-full object-cover"
            />
            <div v-else class="w-full h-full flex items-center justify-center">
              <Icon icon="mdi:music-note" class="w-5 h-5 text-gray-400 dark:text-white/30" />
            </div>
          </div>
          <p
            class="text-xs font-semibold truncate max-w-[120px] text-gray-800 dark:text-white/85 m-0"
          >
            {{ activeFileName ?? "Unknown" }}
          </p>
        </div>

        <!-- Strip: prev -->
        <button
          v-show="isStrip"
          class="w-9 flex-shrink-0 flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          :disabled="!hasPrevious"
          @click="store.previous()"
        >
          <Icon icon="mdi:skip-previous" class="w-6 h-6" />
        </button>

        <!-- Shaka container: never moves, never unmounts -->
        <div
          ref="containerRef"
          class="relative isolate"
          :class="isStrip ? 'flex-1 min-w-0 h-full' : 'w-full h-full'"
          data-shaka-player-container
        >
          <video ref="videoRef" data-shaka-player playsinline disablepictureinpicture />
        </div>

        <!-- Strip: next -->
        <button
          v-show="isStrip"
          class="w-9 flex-shrink-0 flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          :disabled="!hasNext"
          @click="store.next()"
        >
          <Icon icon="mdi:skip-next" class="w-6 h-6" />
        </button>

        <!-- Compact strip: close button (always visible at 40px height) -->
        <button
          v-show="isCompactStrip"
          class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
          @click="store.clearActiveFile()"
        >
          <Icon icon="mdi:close" class="w-5 h-5" />
        </button>

        <!-- Strip: secondary controls + pip toggle + close -->
        <div
          v-show="isStrip && !isCompactStrip"
          class="flex items-center px-3 gap-0.5 border-l border-black/[0.06] dark:border-white/[0.07]"
        >
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
            :class="
              shuffled
                ? 'text-primary'
                : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
            "
            @click="store.toggleShuffle()"
          >
            <Icon icon="mdi:shuffle-variant" class="w-5 h-5" />
          </button>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
            :class="
              repeatMode === 'all' || repeatMode === 'one'
                ? 'text-primary'
                : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
            "
            @click="store.toggleLoop()"
          >
            <Icon :icon="repeatMode === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'" class="w-5 h-5" />
          </button>

          <PlayerQueue />
          <PlayerSettings />
          <button
            class="w-10 h-10 flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 transition-colors"
            @click="cycleMode"
          >
            <Icon :icon="modeIcon" class="w-5 h-5" />
          </button>
          <button
            class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
            @click="store.clearActiveFile()"
          >
            <Icon icon="mdi:close" class="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
@import "shaka-player/dist/controls.css";

.player-card {
  --seek-base: rgba(255, 255, 255, 0.18);
  --seek-buffered: rgba(255, 255, 255, 0.35);
  --seek-played: var(--ui-primary);
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
  backdrop-filter: blur(10px);
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
.player-card.strip [data-shaka-player-container] {
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
.overlay-fade-enter-active,
.overlay-fade-leave-active {
  transition: opacity 220ms ease;
}
.overlay-fade-enter-from,
.overlay-fade-leave-to {
  opacity: 0;
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
  .player-card.strip:not(.compact-strip) > div.relative > div[data-shaka-player-container] {
    height: 72px !important;
    min-height: 72px;
    display: flex;
    align-items: center;
  }

  .player-card.strip:not(.compact-strip) [data-shaka-player-container] {
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
.player-card.strip.compact-strip:hover > div.relative > div[data-shaka-player-container] {
  height: 68px !important;
  min-height: 68px;
}

.player-card.strip.compact-strip:hover [data-shaka-player-container] {
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
