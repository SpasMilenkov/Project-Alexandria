// oxlint-disable max-statements max-lines-per-function
import { type Ref, computed, onUnmounted, ref, watch } from "vue";

import { attemptRefresh } from "@/api/client";
import { type MediaFileDto, streamingApi } from "@/api/streaming";
import { closeSession, startSession } from "@/mutations/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { ListeningHistoryTracker } from "@/utils/listening-history-tracker";

const REFRESH_INTERVAL_MS = 10 * 30 * 1_000;
const RESUME_CUTOFF_FROM_END = 10;
const RESUME_MIN_POSITION = 5;

export interface PlayerEngineOptions {
  getThumbnailUrl?: () => string | null | undefined;
  shakaUiConfig?: Record<string, unknown>;
  mediaKind: "audio" | "video";
  headless?: boolean;
}

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
  controlPanelElements: ["spacer", "mute", "volume"],
};

export const usePlayerEngine = (
  videoRef: Ref<HTMLVideoElement | null>,
  containerRef: Ref<HTMLElement | null>,
  options: PlayerEngineOptions,
) => {
  const store = usePlayerStore();
  const { mutateAsync: endSessionAsync } = closeSession();
  const { mutateAsync: openSessionAsync } = startSession();

  const historyTracker = new ListeningHistoryTracker({
    startSession: (fileId, startPositionSeconds) =>
      openSessionAsync({ fileId, startPositionSeconds }),
    closeSession: (sessionId, payload) => endSessionAsync({ sessionId, req: payload }),
    verifyClosed: async (sessionId, fileId) => {
      const history = await streamingApi.getHistoryByFile(fileId).catch(() => null);
      if (!history) return false;
      const sessions = await streamingApi.getSessions(history.id).catch(() => null);
      return sessions?.items.some((s) => s.id === sessionId && s.endedAt !== null) ?? false;
    },
    currentFileId: () => store.nowPlaying?.file.fileId ?? null,
  });
  const disposeHistoryFlush = store.registerHistoryFlush(() => historyTracker.flush());
  const disposeVideoSurface =
    options.mediaKind === "video" && !options.headless ? store.registerVideoSurface() : null;

  // Every engine hears store changes, but only the one whose kind matches the
  // playing file may report back. Otherwise a paused audio element keeps
  // writing transport state, media session and ended events over the video.
  const isActiveEngine = () =>
    (store.nowPlaying?.file.mimeType.startsWith("audio/") ?? true) ===
    (options.mediaKind === "audio");

  const playerReady = computed(() => store.engineReady);
  const isBuffering = computed(() => store.engineBuffering);
  const loadError = computed(() => store.engineLoadError);
  const resumePrompt = ref<{ positionSeconds: number } | null>(null);

  let shakaPlayer: any = null;
  let shakaUi: any = null;
  let shakaInitPromise: Promise<void> | null = null;
  let disposeEngine: (() => void) | null = null;
  let loadedInstanceId: number | null = null;
  let loadedFileId: string | null = null;
  let loadedVersionId: string | null = null;
  let currentManifestUrl = "";
  let ownsMediaSession = false;

  let pendingResumePosition: number | null = null;
  let pendingLoadSequence = 0;
  let pendingRestored = false;

  let loadSequenceCounter = 0;

  const delayedPlay = (element: HTMLVideoElement | null, sequence: number) => {
    if (!element) return;
    setTimeout(() => {
      if (sequence !== loadSequenceCounter) return;
      const attempt = element.play();
      if (attempt && typeof attempt.catch === "function") {
        attempt.catch((err: unknown) => {
          if ((err as { name?: string })?.name === "AbortError") return;
          if (sequence !== loadSequenceCounter) return;
          store.setEngineLoadError("Playback was blocked. Press play to retry.");
        });
      }
    }, 50);
  };

  const listenedSeconds = ref(0);
  let playbackSessionOpen = false;
  let listenTicker: ReturnType<typeof setInterval> | null = null;
  let refreshTicker: ReturnType<typeof setInterval> | null = null;

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
    listenTicker = setInterval(() => {
      if (
        videoRef.value &&
        !videoRef.value.paused &&
        !videoRef.value.seeking &&
        !isBuffering.value
      ) {
        listenedSeconds.value++;
      }
    }, 1_000);
  };

  const stopListenTicker = () => {
    if (listenTicker === null) return;
    clearInterval(listenTicker);
    listenTicker = null;
  };

  const closeActiveSession = (playbackFinished = false) => {
    if (!playbackSessionOpen) return;

    playbackSessionOpen = false;
    if (!videoRef.value) return;
    stopListenTicker();
    historyTracker.closeActive({
      endPositionSeconds: Math.floor(videoRef.value.currentTime),
      listenedSeconds: listenedSeconds.value,
      playbackFinished: playbackFinished || videoRef.value.ended,
    });
    listenedSeconds.value = 0;
  };

  const openNewSession = () => {
    if (!store.nowPlaying || !videoRef.value) return;

    if (playbackSessionOpen) {
      startListenTicker();

      return;
    }

    closeActiveSession();
    listenedSeconds.value = 0;
    historyTracker.openNew(store.nowPlaying.file.fileId, Math.floor(videoRef.value.currentTime));
    playbackSessionOpen = true;
    startListenTicker();
  };

  const acceptResumePrompt = () => {
    if (resumePrompt.value && videoRef.value) {
      videoRef.value.currentTime = resumePrompt.value.positionSeconds;
    }
    resumePrompt.value = null;
    videoRef.value?.play();
  };

  // Fullscreen through Shaka so the button, double-tap, and Shaka's own
  // gestures all enter and exit the same element. Returns false when the
  // Shaka UI is not ready yet so callers can fall back.
  const toggleShakaFullscreen = (): boolean => {
    const controls = shakaUi?.getControls?.() ?? null;
    const toggle = controls?.toggleFullScreen;
    if (typeof toggle !== "function") return false;
    toggle.call(controls);
    return true;
  };

  const dismissResumePrompt = () => {
    resumePrompt.value = null;
    videoRef.value?.play();
  };

  const syncVariantTracks = () => {
    if (!shakaPlayer) return;
    const tracks = shakaPlayer.getVariantTracks();
    store.setVariantTracks(tracks);
    const active = tracks.find((t: any) => t.active);
    store.setActiveVariantId(store.abrEnabled ? null : (active?.id ?? null));
  };

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
    ownsMediaSession = true;
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
      if (videoRef.value && details.seekTime !== null && details.seekTime !== undefined) {
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
    if (!("mediaSession" in navigator) || !ownsMediaSession) return;
    ownsMediaSession = false;
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

  const initShaka = () => {
    if (shakaInitPromise) return shakaInitPromise;

    shakaInitPromise = (async () => {
      try {
        const shaka = (await import("shaka-player/dist/shaka-player.ui.js")) as any;
        shaka.polyfill?.installAll?.();

        if (!shaka.Player.isBrowserSupported()) {
          store.setEngineLoadError("Browser doesn't support adaptive streaming.");
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

        videoRef.value?.addEventListener("loadedmetadata", () => {
          const el = videoRef.value;
          if (!el || !isActiveEngine()) return;

          const dur = el.duration;
          if (dur && isFinite(dur)) {
            store.setDuration(dur);
          }

          const pos = pendingResumePosition;
          pendingResumePosition = null;
          const wasRestored = pendingRestored;
          pendingRestored = false;

          if (wasRestored) {
            el.pause();
          } else if (
            pos !== null &&
            dur &&
            isFinite(dur) &&
            pos > RESUME_MIN_POSITION &&
            pos < dur - RESUME_CUTOFF_FROM_END
          ) {
            if (options.headless) {
              el.currentTime = pos;
              delayedPlay(el, pendingLoadSequence);
            } else {
              resumePrompt.value = { positionSeconds: pos };
              el.pause();
            }
          } else {
            delayedPlay(el, pendingLoadSequence);
          }
        });

        videoRef.value?.addEventListener("playing", () => {
          if (!isActiveEngine()) {
            videoRef.value?.pause();
            return;
          }
          store.setIsPlaying(true);
          openNewSession();
          syncPositionState();
        });

        videoRef.value?.addEventListener("pause", () => {
          closeActiveSession();
          if (!isActiveEngine()) return;
          store.setIsPlaying(false);
          syncPositionState();
        });

        videoRef.value?.addEventListener("seeking", stopListenTicker);

        videoRef.value?.addEventListener("seeked", () => {
          if (playbackSessionOpen && !videoRef.value?.paused && !isBuffering.value) {
            startListenTicker();
          }
        });

        videoRef.value?.addEventListener("timeupdate", () => {
          if (!isActiveEngine()) return;
          store.setCurrentTime(videoRef.value?.currentTime ?? 0);
          const dur = videoRef.value?.duration;
          if (dur && isFinite(dur)) store.setDuration(dur);
          syncPositionState();
        });

        videoRef.value?.addEventListener("ratechange", () => {
          if (!isActiveEngine()) return;
          store.setPlaybackRateState(videoRef.value?.playbackRate ?? 1);
        });

        videoRef.value?.addEventListener("ended", () => {
          closeActiveSession(true);
          resumePrompt.value = null;
          if (!isActiveEngine()) return;
          store.setIsPlaying(false);
          if (loadedInstanceId !== null) store.handleTrackEnded(loadedInstanceId);
        });

        if (!options.headless && containerRef.value) {
          shakaUi = new shaka.ui.Overlay(shakaPlayer, containerRef.value, videoRef.value);
          shakaUi.configure(options.shakaUiConfig ?? AUDIO_SHAKA_UI_CONFIG);
        }

        shakaPlayer.addEventListener("buffering", (e: any) => {
          store.setEngineBuffering(e.buffering);

          if (e.buffering) {
            stopListenTicker();
          } else if (playbackSessionOpen && !videoRef.value?.paused && !videoRef.value?.seeking) {
            startListenTicker();
          }
        });

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
              store.setEngineLoadError("Session expired. Please refresh the page.");
            }
            return;
          }
          store.setEngineLoadError(err?.message ?? "Playback error.");
          store.setEngineBuffering(false);
        });

        store.setEngineReady(true);
        startRefreshTicker();

        if (videoRef.value) {
          videoRef.value.volume = store.volume;
          videoRef.value.addEventListener("volumechange", () => {
            store.setVolume(videoRef.value?.volume ?? 0.5);
          });
        }

        disposeEngine = store.registerEngine(options.mediaKind, {
          play: () => videoRef.value?.play(),
          pause: () => videoRef.value?.pause(),
          seek: (t) => {
            if (videoRef.value) videoRef.value.currentTime = t;
          },
          setVolume: (v) => {
            if (videoRef.value) videoRef.value.volume = v;
          },
          setPlaybackRate: (rate) => {
            if (videoRef.value) videoRef.value.playbackRate = rate;
          },
          selectVariant: (id) => {
            if (!shakaPlayer) return;
            if (id === null) {
              shakaPlayer.configure("abr.enabled", true);
              store.setAbrEnabled(true);
              store.setActiveVariantId(null);
            } else {
              shakaPlayer.configure("abr.enabled", false);
              const track = shakaPlayer.getVariantTracks().find((t: any) => t.id === id);
              if (track) shakaPlayer.selectVariantTrack(track, false);
              store.setAbrEnabled(false);
              store.setActiveVariantId(id);
            }
          },
        });
      } catch (err: any) {
        store.setEngineLoadError(err?.message ?? "Player failed to initialize.");
        shakaInitPromise = null;
        throw err;
      }
    })();

    return shakaInitPromise;
  };

  // A file of the other kind took over: stop this element, cancel pending
  // loads, and drop its claim on the shared media session.
  const releaseForeign = () => {
    loadSequenceCounter += 1;
    closeActiveSession();
    resumePrompt.value = null;
    pendingResumePosition = null;
    pendingRestored = false;
    loadedInstanceId = null;
    videoRef.value?.pause();
    clearMediaSession();
  };

  const loadFile = async (
    file: MediaFileDto | null,
    instanceId: number | null,
    restored: boolean,
  ) => {
    const fileIsAudio = file?.mimeType.startsWith("audio/") ?? false;
    if (file && (options.mediaKind === "audio") !== fileIsAudio) {
      releaseForeign();
      return;
    }

    const loadSequence = ++loadSequenceCounter;
    closeActiveSession();
    store.setEngineLoadError(null);
    resumePrompt.value = null;
    pendingResumePosition = null;
    pendingRestored = restored;
    currentManifestUrl = "";
    store.setVariantTracks([]);
    store.setActiveVariantId(null);
    store.setAbrEnabled(true);

    if (!file || instanceId === null) {
      loadedInstanceId = null;
      loadedFileId = null;
      loadedVersionId = null;
      return;
    }

    const versionId = file.playbackVersionId ?? file.currentVersionId;
    if (loadedFileId === file.fileId && loadedVersionId === versionId && shakaPlayer) {
      loadedInstanceId = instanceId;
      if (videoRef.value) {
        videoRef.value.currentTime = 0;
        delayedPlay(videoRef.value, loadSequence);
      }
      updateMediaSession(file);
      return;
    }

    try {
      await initShaka();
      if (!shakaPlayer) return;
      if (loadSequence !== loadSequenceCounter) return;

      const [manifestUrl, history] = await Promise.all([
        streamingApi.getManifest(file.playbackVersionId ?? file.currentVersionId),
        streamingApi.getHistoryByFile(file.fileId).catch(() => null),
      ]);

      if (!manifestUrl) {
        store.setEngineLoadError("Failed to resolve stream URL.");
        return;
      }
      if (loadSequence !== loadSequenceCounter) return;

      currentManifestUrl = manifestUrl;
      loadedInstanceId = instanceId;
      loadedFileId = file.fileId;
      loadedVersionId = versionId;
      shakaPlayer.configure("abr.enabled", true);

      const savedPosition = history?.positionSeconds ?? 0;

      if (restored) {
        await shakaPlayer.load(manifestUrl, savedPosition > 0 ? savedPosition : null);
      } else if (fileIsAudio) {
        await shakaPlayer.load(manifestUrl, null);
        if (loadSequence !== loadSequenceCounter) return;
        delayedPlay(videoRef.value, loadSequence);
      } else {
        pendingLoadSequence = loadSequence;
        if (savedPosition > RESUME_MIN_POSITION) pendingResumePosition = savedPosition;
        await shakaPlayer.load(manifestUrl, null);
      }

      syncVariantTracks();
      updateMediaSession(file);
    } catch (err: any) {
      if (loadSequence !== loadSequenceCounter) return;
      // Report the instance this load was for. loadedInstanceId is only set
      // after the manifest resolves, so it is stale or null on a failed load.
      const notFound =
        err?.response?.status === 404 || (err?.code === 1001 && err?.data?.[1] === 404);
      if (notFound) {
        await store.reportUnavailable(instanceId);
        return;
      }
      store.setEngineLoadError(err?.message ?? "Failed to load stream.");
    }
  };

  watch(
    () => store.nowPlaying?.instanceId ?? null,
    (instanceId) => {
      const playing = store.nowPlaying;
      void loadFile(playing?.file ?? null, instanceId, playing?.restored ?? false);
    },
    { immediate: true },
  );

  // Both elements share one volume, so switching kinds never jumps loudness.
  watch(
    () => store.volume,
    (value) => {
      const element = videoRef.value;
      if (element && element.volume !== value) element.volume = value;
    },
  );

  onUnmounted(async () => {
    clearMediaSession();
    closeActiveSession();
    disposeHistoryFlush();
    disposeVideoSurface?.();
    disposeEngine?.();
    stopRefreshTicker();
    if (shakaUi) {
      shakaUi.destroy();
      shakaUi = null;
    }
    if (shakaPlayer) {
      await shakaPlayer.destroy();
      shakaPlayer = null;
    }
  });

  return {
    isBuffering,
    loadError,
    playerReady,
    resumePrompt,
    acceptResumePrompt,
    dismissResumePrompt,
    toggleShakaFullscreen,
    flushListeningHistory: () => historyTracker.flush(),
    historyFailure: computed(() => historyTracker.failureMessage),
  };
};
