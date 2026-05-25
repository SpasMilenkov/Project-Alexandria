<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute } from "vue-router";

import { attemptRefresh } from "@/api/client";
import { type MediaFileDto, streamingApi } from "@/api/streaming";
import PlayerQueue from "@/components/streaming/PlayerQueue.vue";
import { closeSession, startSession } from "@/mutations/streaming";
import { getPreview } from "@/queries/files";
import { usePlayerStore } from "@/stores/stream-player";
const route = useRoute();
const isCompactStrip = computed(
  () => isStrip.value && route.path !== "/streaming/music" && !route.path.includes("/streaming"),
);

const store = usePlayerStore();
const { activeFile, isAudio, snapCorner, hasNext, hasPrevious, loop } = storeToRefs(store);
const { mutate: openSession, mutateAsync: openSessionAsync } = startSession();
const { mutate: endSession } = closeSession();

// UI state

const playerReady = ref(false);
const isBuffering = ref(false);
const loadError = ref<string | null>(null);

type PlayerMode = "expanded" | "pip" | "strip";
const playerMode = ref<PlayerMode>("strip");
const isMinimized = computed(() => playerMode.value === "pip");
const isStrip = computed(() => playerMode.value === "strip");

// Display-only queries — never drive side effects
const { data: preview } = useQuery(() => getPreview(activeFile.value?.fileId ?? ""));
const audioBg = computed(() => preview.value?.thumbnailUrl ?? null);
const activeFileName = computed(() => activeFile.value?.fileName ?? null);

// Session tracking

const activeSessionId = ref<string | null>(null);
const listenedSeconds = ref(0);
let listenTicker: ReturnType<typeof setInterval> | null = null;
let refreshTicker: ReturnType<typeof setInterval> | null = null;
const REFRESH_INTERVAL_MS = 10 * 60 * 1_000;

const startRefreshTicker = () => {
  if (refreshTicker !== null) return;
  refreshTicker = setInterval(() => attemptRefresh(), REFRESH_INTERVAL_MS);
};

const stopRefreshTicker = () => {
  if (refreshTicker === null) return;
  clearInterval(refreshTicker);
  refreshTicker = null;
};

const startListenTicker = () => {
  if (listenTicker !== null) return;
  startRefreshTicker();
  listenTicker = setInterval(() => {
    if (videoRef.value && !videoRef.value.paused && !isBuffering.value) {
      listenedSeconds.value++;
    }
  }, 1_000);
};

const stopListenTicker = () => {
  stopRefreshTicker();
  if (listenTicker === null) return;
  clearInterval(listenTicker);
  listenTicker = null;
};

const openNewSession = async () => {
  if (!activeFile.value || !videoRef.value) return;
  closeActiveSession();
  listenedSeconds.value = 0;
  const session = await openSessionAsync({
    fileId: activeFile.value.fileId,
    startPositionSeconds: Math.floor(videoRef.value.currentTime),
  });
  activeSessionId.value = session?.id ?? null;
  startListenTicker();
};

const closeActiveSession = () => {
  if (!activeSessionId.value || !videoRef.value) return;
  stopListenTicker();
  endSession({
    sessionId: activeSessionId.value,
    req: {
      endPositionSeconds: Math.floor(videoRef.value.currentTime),
      listenedSeconds: listenedSeconds.value,
    },
  });
  activeSessionId.value = null;
  listenedSeconds.value = 0;
};

// Media Session API

const updateMediaSession = (file: MediaFileDto | null) => {
  if (!("mediaSession" in navigator) || !file) return;

  navigator.mediaSession.metadata = new MediaMetadata({
    title: file.fileName,
    // artwork pulled from the same preview thumbnail the player already uses
    artwork: preview.value?.thumbnailUrl
      ? [{ src: preview.value.thumbnailUrl, sizes: "512x512", type: "image/jpeg" }]
      : [],
  });

  navigator.mediaSession.setActionHandler("play", () => videoRef.value?.play());
  navigator.mediaSession.setActionHandler("pause", () => videoRef.value?.pause());

  navigator.mediaSession.setActionHandler("previoustrack", () => {
    if (store.hasPrevious) store.previous();
  });
  navigator.mediaSession.setActionHandler("nexttrack", () => {
    if (store.hasNext) store.next();
  });

  // Seek support — lets the OS scrubber / +10s / -10s buttons work
  navigator.mediaSession.setActionHandler("seekto", (details) => {
    if (videoRef.value && details.seekTime != null) {
      videoRef.value.currentTime = details.seekTime;
      syncPositionState();
    }
  });
  navigator.mediaSession.setActionHandler("seekforward", (details) => {
    if (videoRef.value) {
      videoRef.value.currentTime += details.seekOffset ?? 10;
      syncPositionState();
    }
  });
  navigator.mediaSession.setActionHandler("seekbackward", (details) => {
    if (videoRef.value) {
      videoRef.value.currentTime -= details.seekOffset ?? 10;
      syncPositionState();
    }
  });
};

const syncPositionState = () => {
  if (!("mediaSession" in navigator) || !videoRef.value) return;
  const { duration, currentTime, playbackRate, paused } = videoRef.value;
  // playbackState lives here instead of scattered inline
  navigator.mediaSession.playbackState = paused ? "paused" : "playing";
  if (duration && isFinite(duration)) {
    navigator.mediaSession.setPositionState({ duration, position: currentTime, playbackRate });
  }
};

const clearMediaSession = () => {
  if (!("mediaSession" in navigator)) return;
  navigator.mediaSession.metadata = null;
  navigator.mediaSession.playbackState = "none";
  (
    [
      "play",
      "pause",
      "previoustrack",
      "nexttrack",
      "seekto",
      "seekforward",
      "seekbackward",
    ] as const
  ).forEach((a) => navigator.mediaSession.setActionHandler(a, null));
};

// Drag / snap (pip mode)

const isDragging = ref(false);
const cardPos = ref({ x: 0, y: 0 });
const CARD_W = 320;
const MARGIN = 24;

const cardRef = ref<HTMLDivElement | null>(null);
const containerRef = ref<HTMLDivElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);

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

// Mode cycling

const modeIcon = computed(() => {
  if (playerMode.value === "expanded") return "mdi:picture-in-picture-bottom-right";
  if (playerMode.value === "pip") return isAudio.value ? "mdi:dock-bottom" : "mdi:arrow-expand";
  return "mdi:arrow-expand";
});

// expanded -> pip -> strip (audio only) -> expanded
const cycleMode = async () => {
  if (playerMode.value === "expanded") {
    const rect = cardRef.value!.getBoundingClientRect();
    cardPos.value = { x: rect.left, y: rect.top };
    playerMode.value = "pip";
    await nextTick();
    requestAnimationFrame(() => snapTo("br"));
  } else if (playerMode.value === "pip") {
    playerMode.value = isAudio.value ? "strip" : "expanded";
  } else {
    playerMode.value = "expanded";
  }
};

// Shaka

let shakaPlayer: any = null;
let shakaUi: any = null;
let shakaInitPromise: Promise<void> | null = null;

// Tracks whether this is the very first file loaded since mount.
// On first load we do not autoplay — the user must press play.
// On subsequent loads (queue navigation) we autoplay.
let isFirstLoad = true;

// The manifest URL that is currently loaded, kept only so the auth-error
// recovery path can re-call shakaPlayer.load() without another fetch.
let currentManifestUrl = "";

const initShaka = () => {
  if (shakaInitPromise) return shakaInitPromise;

  // oxlint-disable-next-line max-statements
  shakaInitPromise = (async () => {
    try {
      const shaka = (await import("shaka-player/dist/shaka-player.ui.js")) as any;
      shaka.polyfill?.installAll?.();

      if (!shaka.Player.isBrowserSupported()) {
        loadError.value = "Browser doesn't support adaptive streaming.";
        return;
      }

      if (shakaUi) {
        shakaUi.destroy();
        shakaUi = null;
      }
      if (shakaPlayer) {
        await shakaPlayer.destroy();
        shakaPlayer = null;
      }

      shakaPlayer = new shaka.Player();
      await shakaPlayer.attach(videoRef.value);

      videoRef.value?.addEventListener("playing", () => {
        openNewSession();
        syncPositionState();
      });

      videoRef.value?.addEventListener("pause", () => {
        closeActiveSession();
        syncPositionState();
      });

      videoRef.value?.addEventListener("timeupdate", syncPositionState);

      videoRef.value?.addEventListener("ended", () => {
        closeActiveSession();
        if (store.autoplay && (store.hasNext || store.loop)) {
          store.next();
        }
      });

      shakaUi = new shaka.ui.Overlay(shakaPlayer, containerRef.value, videoRef.value);
      shakaUi.configure({
        controlPanelElements: [
          "play_pause",
          "time_and_duration",
          "spacer",
          "mute",
          "volume",
          "quality",
        ],
        addSeekBar: true,
        fadeDelay: 3,
        enableTooltips: true,
        seekBarColors: {
          base: "rgba(255,255,255,0.18)",
          buffered: "rgba(255,255,255,0.35)",
          played: "rgb(99,102,241)",
        },
      });

      shakaPlayer.addEventListener("buffering", (e: any) => {
        isBuffering.value = e.buffering;
      });

      shakaPlayer.addEventListener("error", async (e: any) => {
        const err = e.detail;
        const isAuthError = err?.code === 1001 && err?.data?.[1] === 401;

        if (isAuthError && videoRef.value && currentManifestUrl) {
          const resumeAt = videoRef.value.currentTime;
          const wasPaused = videoRef.value.paused;
          try {
            await attemptRefresh();
            await shakaPlayer.load(currentManifestUrl, resumeAt);

            if (!wasPaused) videoRef.value.play();
          } catch {
            loadError.value = "Session expired. Please refresh the page.";
          }
          return;
        }

        loadError.value = err?.message ?? "Playback error.";
        isBuffering.value = false;
      });

      playerReady.value = true;

      if (videoRef.value) {
        videoRef.value.volume = store.volume;
        videoRef.value.addEventListener("volumechange", () => {
          store.setVolume(videoRef.value?.volume ?? 0.5);
        });
      }
    } catch (err: any) {
      loadError.value = err?.message ?? "Player failed to initialize.";
      shakaInitPromise = null;
      throw err;
    }
  })();

  return shakaInitPromise;
};

// Core load function
//
// This is the single entry point for loading a new file. It owns the full
// sequence: close session → fetch manifest + history in parallel → load shaka
// → autoplay if this is not the initial mount load.
//
// By fetching the manifest imperatively here rather than through a reactive
// query, focus/blur events that cause @pinia/colada to refetch the manifest
// cache have zero effect on playback: there is no watcher listening to the
// query result anymore.

const loadFile = async (file: MediaFileDto | null) => {
  closeActiveSession();
  loadError.value = null;
  currentManifestUrl = "";

  if (!file) return;

  try {
    await initShaka();
    if (!shakaPlayer) return;

    const [manifestUrl, history] = await Promise.all([
      streamingApi.getManifest(file.currentVersion.id),
      streamingApi.getHistoryByFile(file.fileId),
    ]);

    if (!manifestUrl) {
      loadError.value = "Failed to resolve stream URL.";
      return;
    }

    currentManifestUrl = manifestUrl;

    // Resume logic:
    //   - First mount load: always resume from saved position (any media type).
    //   - Queue navigation on audio: start from 0 (track-based, not positional).
    //   - Queue navigation on video: resume from saved position.
    const shouldResume = isFirstLoad || !file.mimeType.startsWith("audio/");
    const startTime = shouldResume ? (history?.positionSeconds ?? 0) : 0;

    await shakaPlayer.load(manifestUrl, startTime > 0 ? startTime : null);

    updateMediaSession(file);
    if (!isFirstLoad) {
      // Queue navigation: autoplay the new track.
      setTimeout(() => videoRef.value?.play(), 50);
    }

    isFirstLoad = false;
  } catch (err: any) {
    loadError.value = err?.message ?? "Failed to load stream.";
  }
};

// Single source of truth: when the active file changes, load it.
watch(activeFile, (file) => loadFile(file), { immediate: true });

// Visibility

const onVisibilityChange = () => {
  if (document.hidden) closeActiveSession();
};

// Lifecycle

onMounted(async () => {
  await nextTick();
  requestAnimationFrame(() => snapTo(snapCorner.value));
  window.addEventListener("resize", onResize);
  document.addEventListener("visibilitychange", onVisibilityChange);
});

onUnmounted(async () => {
  closeActiveSession();
  if (shakaUi) {
    shakaUi.destroy();
    shakaUi = null;
  }
  if (shakaPlayer) {
    await shakaPlayer.destroy();
    shakaPlayer = null;
  }
  document.removeEventListener("visibilitychange", onVisibilityChange);
  window.removeEventListener("resize", onResize);
  window.removeEventListener("mousemove", onMouseMove);
  window.removeEventListener("mouseup", onMouseUp);
});

watch(playerMode, (m) => store.setPlayerMode(m), { immediate: true });
</script>

<template>
  <!--
    Outer wrapper:
    - expanded: normal page flow, centered
    - pip: h-0 so the fixed card doesn't push content
    - strip: sticky bottom-0 so it sticks to the bottom of the parent
      scrolling container rather than the viewport, respecting the parent width
  -->
  <div
    :class="{
      'flex items-start justify-center p-8': playerMode === 'expanded',
      'h-0': playerMode === 'pip',
      'absolute bottom-0 w-full z-[9999]': playerMode === 'strip',
    }"
  >
    <div
      ref="cardRef"
      class="player-card bg-white/75 dark:bg-white/[0.06] backdrop-blur-xl border border-black/[0.08] dark:border-white/10 overflow-hidden"
      :class="[
        isStrip ? 'w-full rounded-t-xl' : 'w-full max-w-[900px] rounded-2xl',
        {
          minimized: isMinimized,
          strip: isStrip,
          'is-dragging': isDragging,
          'compact-strip': isCompactStrip,
        },
      ]"
      :style="cardStyle"
    >
      <!-- Header: hidden in strip via v-show (stays in DOM, zero height) -->
      <div
        v-show="!isStrip"
        class="flex items-center justify-between border-b border-gray-200/70 dark:border-white/[0.07] select-none"
        :class="[
          isMinimized ? 'px-3 py-2' : 'px-6 py-4',
          isMinimized ? (isDragging ? 'cursor-grabbing' : 'cursor-grab') : '',
        ]"
        @mousedown="onMouseDown"
      >
        <div class="flex items-center gap-2.5 min-w-0">
          <div class="min-w-0">
            <p class="font-semibold truncate text-gray-800 dark:text-white/90">
              {{ activeFileName ?? "Live Stream" }}
            </p>
          </div>
        </div>

        <div class="flex items-center gap-1 flex-shrink-0" @mousedown.stop>
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            :disabled="!hasPrevious"
            @click="store.previous()"
          >
            <Icon icon="mdi:skip-previous" class="w-4 h-4" />
          </button>
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            :disabled="!hasNext"
            @click="store.next()"
          >
            <Icon icon="mdi:skip-next" class="w-4 h-4" />
          </button>
          <button class="w-7 h-7 rounded-lg" @click="cycleMode">
            <Icon :icon="modeIcon" class="w-4 h-4" />
          </button>
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
            @click="store.shuffle()"
          >
            <Icon icon="mdi:shuffle-variant" class="w-4 h-4" />
          </button>
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center transition-colors"
            :class="
              loop
                ? 'text-primary'
                : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
            "
            @click="store.toggleLoop()"
          >
            <Icon icon="mdi:repeat" class="w-4 h-4" />
          </button>
          <PlayerQueue />
        </div>
      </div>

      <!--
        Player area.

        Non-strip: bg-black, aspect-video, overflow-hidden (clips the artwork bleed).
        Strip: flex row, fixed height, NO overflow-hidden so Shaka tooltips are not clipped.

        containerRef is always "relative" so Shaka's absolute-positioned overlay
        is always anchored correctly. Width/height change by class swap only.
        The ref itself never moves or unmounts.
      -->
      <div
        class="relative"
        :class="
          isStrip
            ? 'flex items-stretch h-[72px]'
            : 'bg-black rounded-b-2xl aspect-video overflow-hidden'
        "
      >
        <!-- Audio artwork (expanded / pip only) -->
        <Transition name="overlay-fade">
          <div
            v-if="isAudio && audioBg && !isStrip"
            class="absolute inset-0 overflow-hidden pointer-events-none"
            aria-hidden="true"
          >
            <img
              :src="audioBg"
              alt=""
              class="absolute inset-0 w-full h-full object-cover scale-110 blur-2xl opacity-40"
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
              <Icon icon="mdi:music-note" class="w-4 h-4 text-gray-400 dark:text-white/30" />
            </div>
          </div>
          <p class="text-xs font-semibold truncate text-gray-800 dark:text-white/85 m-0">
            {{ activeFileName ?? "Unknown" }}
          </p>
        </div>

        <!-- Strip: prev button -->
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
          <video
            ref="videoRef"
            class="absolute inset-0 w-full h-full block object-contain transition-opacity duration-200"
            :class="{ 'opacity-0 pointer-events-none': isStrip }"
            data-shaka-player
            playsinline
          />
        </div>

        <!-- Strip: next button -->
        <button
          v-show="isStrip"
          class="w-9 flex-shrink-0 flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          :disabled="!hasNext"
          @click="store.next()"
        >
          <Icon icon="mdi:skip-next" class="w-6 h-6" />
        </button>

        <!-- Strip: mode toggle -->
        <div
          v-show="isStrip && !isCompactStrip"
          class="flex items-center px-3 border-l border-black/[0.06] dark:border-white/[0.07]"
        >
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80 transition-colors"
            @click="store.shuffle()"
          >
            <Icon icon="mdi:shuffle-variant" class="w-4 h-4" />
          </button>
          <button
            class="w-7 h-7 rounded-lg flex items-center justify-center transition-colors"
            :class="
              loop
                ? 'text-primary'
                : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
            "
            @click="store.toggleLoop()"
          >
            <Icon icon="mdi:repeat" class="w-4 h-4" />
          </button>
          <PlayerQueue />
          
          <button
            class="w-7 h-7 flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-gray-800 dark:hover:text-white/90 transition-colors"
            @click="cycleMode"
          >
            <Icon :icon="modeIcon" class="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
@import "shaka-player/dist/controls.css";

.shaka-video-container {
  background: transparent !important;
  isolation: isolate;
}

.shaka-controls-container {
  z-index: 30 !important;
  pointer-events: auto !important;
}

.shaka-bottom-controls {
  z-index: 31 !important;
}

.shaka-video-container video {
  pointer-events: auto !important;
}

/* BOTTOM CONTROLS GRADIENT */

.shaka-bottom-controls {
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.5) 0%,
    rgba(0, 0, 0, 0.18) 55%,
    transparent 100%
  );
  padding: 0 16px 12px;
}

/* STRIP OVERRIDES */

.player-card.strip .shaka-bottom-controls {
  background: none !important;
  padding: 2px 6px 4px !important;
}

.player-card.strip .shaka-controls-container {
  background: transparent !important;
}

.player-card.strip [data-shaka-player-container] {
  overflow: hidden;
}

/* Tooltips still need to escape — re-allow just for them */
.player-card.strip .shaka-tooltip {
  overflow: visible;
  bottom: calc(100% + 6px) !important;
  top: auto !important;
}

/* Keep controls permanently visible in strip; Shaka fades them on mouse idle */

.player-card.strip .shaka-controls-container,
.player-card.strip .shaka-controls-container[shown="false"] {
  opacity: 1 !important;
}

/*
  Scale down Shaka's control elements for the 72px strip height.
*/

.player-card.strip .shaka-play-button {
  width: 34px !important;
  height: 34px !important;
  padding: 0 !important;
}

.player-card.strip .shaka-controls-button-panel {
  align-items: center;
  gap: 2px;
}

.shaka-asset-title {
  display: none !important;
}

.player-card.strip .shaka-controls-button-panel .material-icons-round,
.player-card.strip .shaka-play-button .material-icons-round {
  font-size: 18px !important;
}

/* Volume bar in strip: keep it but compact */

.player-card.strip .shaka-volume-bar-container {
  max-width: 60px !important;
}

.shaka-ui-icon {
  width: 2rem;
  height: 2rem;
}

.strip .shaka-ui-icon {
  width: 1.25rem;
  height: 1.25rem;
}

.shaka-content-title {
  display: none !important;
}

/*
  Tooltips: allow them to overflow the strip container upward.
*/

.player-card.strip .shaka-tooltip {
  bottom: calc(100% + 6px) !important;
  top: auto !important;
}

.shaka-controls-button-panel {
  align-items: center;
  gap: 4px;
}

/* PLAY BUTTON */

.shaka-play-button {
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.14);
  border: 1px solid rgba(255, 255, 255, 0.18);
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(10px);
  transition:
    background 160ms ease,
    transform 160ms ease;
}

.shaka-play-button:hover {
  background: rgba(var(--ui-primary), 0.7);
  transform: scale(1.04);
}

.player-card.strip .shaka-play-button {
  background: rgba(128, 128, 128, 0.12);
  border-color: rgba(128, 128, 128, 0.2);
  box-shadow: none;
}

/* VOLUME */

.player-card.minimized .shaka-volume-bar-container {
  display: none;
}

.shaka-volume-bar-container {
  margin-left: 0;
}

.shaka-overflow-menu.shaka-overflow-menu-shown,
.shaka-settings-menu.shaka-displayed {
  pointer-events: auto;
}

/* TRANSITIONS */

.overlay-fade-enter-active,
.overlay-fade-leave-active {
  transition: opacity 220ms ease;
}

.overlay-fade-enter-from,
.overlay-fade-leave-to {
  opacity: 0;
}

.shaka-scrim-container {
  background: transparent !important;
}

@media (max-width: 475px) {
  /* ── Strip: art/title wraps to a full-width top row ──────────── */

  /*
   * The player-area div gets Tailwind's `flex`, `items-stretch`,
   * and `h-[72px]` classes when isStrip=true.  We allow wrap here
   * so the first child (art + title) breaks onto its own row, and
   * !important overrides the fixed height so the card can grow.
   */
  .player-card.strip > div.relative {
    flex-wrap: wrap;
    height: auto !important;
  }

  /* Art + title panel → full-width, stacked on top */
  .player-card.strip > div.relative > div:first-child {
    width: 100% !important;
    max-width: 100% !important;
    flex-shrink: 0;
    border-right: none !important;
    border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  }

  .dark .player-card.strip > div.relative > div:first-child {
    border-bottom-color: rgba(255, 255, 255, 0.07);
  }

  /*
   * The remaining children (prev button, shaka container, next button,
   * mode-toggle div) share the second row.  Give them an explicit height
   * so the row is always 64 px regardless of content.
   */
  .player-card.strip > div.relative > button,
  .player-card.strip > div.relative > div[data-shaka-player-container],
  .player-card.strip > div.relative > div:last-child {
    height: 64px;
    display: flex;
    align-items: center;
  }

  /* ── PiP mini-player: smaller card + square for audio ────────── */

  /*
   * cardStyle sets `width: 320px` as an inline style; !important wins.
   * Snap positions will be ~80 px off on the trailing edge, which is
   * acceptable — JS snap math can't be touched without breaking Shaka.
   */
  .player-card.minimized {
    width: 240px !important;
  }

  /*
   * Square aspect ratio for the video/artwork area.
   * Tailwind's `aspect-video` sets aspect-ratio: 16/9; override it here.
   * Because this is purely visual and applies to both audio and video,
   * it makes the pip feel intentional as a "thumbnail" on small screens.
   */
  .player-card.minimized .aspect-video {
    aspect-ratio: 1 / 1 !important;
  }
}

/*
 * Collapse the card itself rather than an inner div — avoids fighting
 * Tailwind's h-[72px] and keeps the tooltip / overflow story simple.
 * The card is the natural clip boundary; on hover it expands to full height.
 */
.player-card.strip.compact-strip {
  height: 40px;
  overflow: hidden;
  transition:
    height 240ms cubic-bezier(0.4, 0, 0.2, 1),
    overflow 0ms 240ms; /* delay restoring overflow until after collapse */
}

.player-card.strip.compact-strip:hover {
  height: 72px;
  overflow: visible;
  transition: height 240ms cubic-bezier(0.4, 0, 0.2, 1);
}

/* Art thumbnail: collapse to zero width */
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

/* Info panel: drop fixed w-52, let it breathe, remove divider */
.player-card.strip.compact-strip > div.relative > div:first-child {
  width: auto !important;
  max-width: 35%;
  border-right: none !important;
}

/* Prev / next buttons: collapse to zero */
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
  width: 2.25rem !important; /* w-9 */
  opacity: 1;
  pointer-events: auto;
}

/* Mode toggle: same treatment */
.player-card.strip.compact-strip > div.relative > div:last-child {
  width: 0 !important;
  opacity: 0;
  overflow: hidden;
  padding: 0 !important;
  border: none !important;
  pointer-events: none;
  transition:
    width 240ms ease,
    opacity 180ms ease;
}

.player-card.strip.compact-strip:hover > div.relative > div:last-child {
  width: auto !important;
  opacity: 1;
  padding: 0 0.75rem !important;
  pointer-events: auto;
}

/* Shaka button panel: fade out, leave seek bar as the sole control */
.player-card.strip.compact-strip .shaka-controls-button-panel {
  opacity: 0;
  pointer-events: none;
  transition: opacity 180ms ease;
}

.player-card.strip.compact-strip:hover .shaka-controls-button-panel {
  opacity: 1;
  pointer-events: auto;
}

/* Nudge the seek bar to sit nicely in the 40px height */
.player-card.strip.compact-strip .shaka-bottom-controls {
  padding-bottom: 4px !important;
}
</style>
