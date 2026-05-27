import { onMounted, onUnmounted, watch, ref, type Ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { attemptRefresh } from "@/api/client";
import { streamingApi } from "@/api/streaming";
import { closeSession, startSession } from "@/mutations/streaming";
import { usePlayerStore } from "@/stores/stream-player";

const REFRESH_INTERVAL_MS = 10 * 60 * 1_000;

export interface PlayerEngineOptions {
  getThumbnailUrl?: () => string | null | undefined;
  shakaUiConfig?: Record<string, unknown>;
}

// Quality is intentionally excluded from both skin configs — it's handled by
// PlayerSettings.vue so the menu is never clipped by strip overflow constraints.
const BASE_SHAKA_UI_CONFIG = {
  addSeekBar: true,
  fadeDelay: 3,
  enableTooltips: true,
  seekBarColors: {
    base: "var(--seek-base)",
    buffered: "var(--seek-buffered)",
    played: "var(--seek-played)",
  },
};
export const AUDIO_SHAKA_UI_CONFIG = {
  ...BASE_SHAKA_UI_CONFIG,
  controlPanelElements: ["play_pause", "time_and_duration", "spacer", "mute", "volume"],
};

export const VIDEO_SHAKA_UI_CONFIG = {
  ...BASE_SHAKA_UI_CONFIG,
  controlPanelElements: [
    "play_pause",
    "time_and_duration",
    "spacer",
    "mute",
    "volume",
    "fullscreen",
  ],
};

export function usePlayerEngine(
  videoRef: Ref<HTMLVideoElement | null>,
  containerRef: Ref<HTMLElement | null>,
  options: PlayerEngineOptions = {},
) {
  const store = usePlayerStore();
  const { mutate: endSession } = closeSession();
  const { mutateAsync: openSessionAsync } = startSession();

  const playerReady = ref(false);
  const isBuffering = ref(false);
  const loadError = ref<string | null>(null);

  let shakaPlayer: any = null;
  let shakaUi: any = null;
  let shakaInitPromise: Promise<void> | null = null;
  let isFirstLoad = true;
  let currentManifestUrl = "";

  const activeSessionId = ref<string | null>(null);
  const listenedSeconds = ref(0);
  let listenTicker: ReturnType<typeof setInterval> | null = null;
  let refreshTicker: ReturnType<typeof setInterval> | null = null;

  // Session tracking

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

  const openNewSession = async () => {
    if (!store.activeFile || !videoRef.value) return;
    closeActiveSession();
    listenedSeconds.value = 0;
    const session = await openSessionAsync({
      fileId: store.activeFile.fileId,
      startPositionSeconds: Math.floor(videoRef.value.currentTime),
    });
    activeSessionId.value = session?.id ?? null;
    startListenTicker();
  };

  // Quality management

  const syncVariantTracks = () => {
    if (!shakaPlayer) return;
    const tracks = shakaPlayer.getVariantTracks();
    store.setVariantTracks(tracks);
    const active = tracks.find((t: any) => t.active);
    // Only report a specific active ID when ABR is disabled — when ABR is on,
    // activeVariantId stays null so the settings panel shows "Auto".
    store.setActiveVariantId(store.abrEnabled ? null : (active?.id ?? null));
  };

  // Media Session API

  const syncPositionState = () => {
    if (!("mediaSession" in navigator) || !videoRef.value) return;
    const { duration, currentTime, playbackRate, paused } = videoRef.value;
    navigator.mediaSession.playbackState = paused ? "paused" : "playing";
    if (duration && isFinite(duration)) {
      navigator.mediaSession.setPositionState({ duration, position: currentTime, playbackRate });
    }
  };

  const updateMediaSession = (file: MediaFileDto | null) => {
    if (!("mediaSession" in navigator) || !file) return;
    const thumbnailUrl = options.getThumbnailUrl?.();
    navigator.mediaSession.metadata = new MediaMetadata({
      title: file.title ?? file.fileName,
      artist: file.artist ?? undefined,
      album: file.album ?? undefined,
      artwork: thumbnailUrl ? [{ src: thumbnailUrl, sizes: "512x512", type: "image/jpeg" }] : [],
    });
    navigator.mediaSession.setActionHandler("play", () => videoRef.value?.play());
    navigator.mediaSession.setActionHandler("pause", () => videoRef.value?.pause());
    navigator.mediaSession.setActionHandler("previoustrack", () => {
      if (store.hasPrevious) store.previous();
    });
    navigator.mediaSession.setActionHandler("nexttrack", () => {
      if (store.hasNext) store.next();
    });
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

  // Shaka init

  const initShaka = () => {
    if (shakaInitPromise) return shakaInitPromise;

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
          store.setIsPlaying(true);
          openNewSession();
          syncPositionState();
        });
        videoRef.value?.addEventListener("pause", () => {
          store.setIsPlaying(false);
          closeActiveSession();
          syncPositionState();
        });
        videoRef.value?.addEventListener("timeupdate", () => {
          store.setCurrentTime(videoRef.value?.currentTime ?? 0);
          store.setDuration(videoRef.value?.duration ?? 0);
          syncPositionState();
        });
        videoRef.value?.addEventListener("ratechange", () => {
          store.setPlaybackRateState(videoRef.value?.playbackRate ?? 1);
        });
        videoRef.value?.addEventListener("ended", () => {
          store.setIsPlaying(false);
          closeActiveSession();
          if (store.autoplay && (store.hasNext || store.loop)) store.next();
        });

        shakaUi = new shaka.ui.Overlay(shakaPlayer, containerRef.value, videoRef.value);
        shakaUi.configure(options.shakaUiConfig ?? AUDIO_SHAKA_UI_CONFIG);

        shakaPlayer.addEventListener("buffering", (e: any) => {
          isBuffering.value = e.buffering;
        });

        // Sync tracks whenever ABR picks a new variant or the manifest changes.
        shakaPlayer.addEventListener("adaptation", syncVariantTracks);
        shakaPlayer.addEventListener("trackschanged", syncVariantTracks);

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

        // Register engine controls in the store so presentational components
        // (ExpandedPlayerCard, PlayerSettings) can drive playback without
        // holding a reference to videoRef or shakaPlayer themselves.
        store.registerEngine({
          play: () => videoRef.value?.play(),
          pause: () => videoRef.value?.pause(),
          seek: (t) => {
            if (videoRef.value) videoRef.value.currentTime = t;
          },
          setPlaybackRate: (rate) => {
            if (videoRef.value) videoRef.value.playbackRate = rate;
          },
          selectVariant: (id) => {
            if (!shakaPlayer) return;
            if (id === null) {
              // Re-enable ABR — Shaka will resume automatic quality selection.
              shakaPlayer.configure("abr.enabled", true);
              store.setAbrEnabled(true);
              store.setActiveVariantId(null);
            } else {
              // Lock to a specific track. clearBuffer=false means Shaka waits for
              // the current buffer to drain before switching, avoiding a hard stall.
              shakaPlayer.configure("abr.enabled", false);
              const track = shakaPlayer.getVariantTracks().find((t: any) => t.id === id);
              if (track) shakaPlayer.selectVariantTrack(track, false);
              store.setAbrEnabled(false);
              store.setActiveVariantId(id);
            }
          },
        });
      } catch (err: any) {
        loadError.value = err?.message ?? "Player failed to initialize.";
        shakaInitPromise = null;
        throw err;
      }
    })();

    return shakaInitPromise;
  };

  // Core load function

  const loadFile = async (file: MediaFileDto | null) => {
    closeActiveSession();
    loadError.value = null;
    currentManifestUrl = "";
    // Reset quality state for the new file.
    store.setVariantTracks([]);
    store.setActiveVariantId(null);
    store.setAbrEnabled(true);

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

      // Re-enable ABR on each new file load so a quality lock from the previous
      // track doesn't carry over unexpectedly.
      shakaPlayer.configure("abr.enabled", true);

      const shouldResume = isFirstLoad || !file.mimeType.startsWith("audio/");
      const startTime = shouldResume ? (history?.positionSeconds ?? 0) : 0;

      await shakaPlayer.load(manifestUrl, startTime > 0 ? startTime : null);

      syncVariantTracks();
      updateMediaSession(file);

      if (!isFirstLoad) {
        setTimeout(() => videoRef.value?.play(), 50);
      }
      isFirstLoad = false;
    } catch (err: any) {
      loadError.value = err?.message ?? "Failed to load stream.";
    }
  };

  watch(
    () => store.activeFile,
    (file) => loadFile(file),
    { immediate: true },
  );

  const onVisibilityChange = () => {
    if (document.hidden) closeActiveSession();
  };

  onMounted(() => {
    document.addEventListener("visibilitychange", onVisibilityChange);
  });

  onUnmounted(async () => {
    clearMediaSession();
    closeActiveSession();
    store.unregisterEngine();
    if (shakaUi) {
      shakaUi.destroy();
      shakaUi = null;
    }
    if (shakaPlayer) {
      await shakaPlayer.destroy();
      shakaPlayer = null;
    }
    document.removeEventListener("visibilitychange", onVisibilityChange);
  });

  return { isBuffering, loadError, playerReady };
}
