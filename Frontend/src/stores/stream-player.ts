// oxlint-disable max-statements
// oxlint-disable max-lines-per-function
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

export interface VariantTrack {
  id: number;
  height: number | null;
  width: number | null;
  bandwidth: number;
  videoCodec: string | null;
  audioCodec: string | null;
  active: boolean;
}

type EngineControls = {
  play: () => void;
  pause: () => void;
  seek: (seconds: number) => void;
  selectVariant: (id: number | null) => void;
  setPlaybackRate: (rate: number) => void;
};

type UpNextItem =
  | { kind: "queue"; file: MediaFileDto; queueIndex: number }
  | { kind: "source"; file: MediaFileDto; sourceIndex: number };

/**
 * Describes how to lazily expand the playback source list.
 * Set when playback is started from an infinite-scroll source (e.g. the media
 * library) so that next() can fetch more items before the loaded window runs out.
 */
export interface PlaybackContext {
  fetchPage: (page: number) => Promise<{ items: MediaFileDto[]; totalPages: number }>;
  loadedPage: number;
  totalPages: number;
}

let _engine: EngineControls | null = null;

export const usePlayerStore = defineStore(
  "player",
  () => {
    // Persisted state
    const activeFile = ref<MediaFileDto | null>(null);
    const snapCorner = ref<"tl" | "tr" | "bl" | "br">("br");
    const volume = ref(0.5);
    const queueEnded = ref(false);
    const currentIndex = ref(-1);
    const autoplay = ref(true);
    const videoAutoplay = ref(true);
    const playerMode = ref<"expanded" | "pip" | "strip">("pip");
    const activePlaylistId = ref<string | null>(null);
    const repeatMode = ref<"off" | "all" | "one">("off");
    const shuffled = ref(false);
    const sourceList = ref<MediaFileDto[]>([]);
    const sourceId = ref<string | null>(null);
    const originalSourceList = ref<MediaFileDto[]>([]);
    const userQueue = ref<MediaFileDto[]>([]);
    const loadedPage = ref(0);
    const totalPages = ref(0);

    // Runtime-only playback state
    const currentTime = ref(0);
    const duration = ref(0);
    const isPlaying = ref(false);

    // Runtime-only quality state
    const variantTracks = ref<VariantTrack[]>([]);
    const activeVariantId = ref<number | null>(null);
    const abrEnabled = ref(true);
    const playbackRate = ref(1);

    // Runtime-only autoplay countdown (video only)
    const autoplayCountdown = ref<number | null>(null);
    let _countdownInterval: ReturnType<typeof setInterval> | null = null;
    let _completionTimeout: ReturnType<typeof setTimeout> | null = null;

    /**
     * Runtime-only lazy expansion context.
     * Not persisted — restored on mount by useStreamingMediaContext().
     */
    let _playbackContext: PlaybackContext | null = null;
    const isExpandingSource = ref(false);

    // Computed
    const hasNext = computed(
      () =>
        userQueue.value.length > 0 ||
        isExpandingSource.value ||
        (_playbackContext !== null && _playbackContext.loadedPage < _playbackContext.totalPages) ||
        (repeatMode.value !== "off"
          ? sourceList.value.length > 1
          : currentIndex.value < sourceList.value.length - 1),
    );

    const hasPrevious = computed(() => currentIndex.value > 0);

    const upNextItems = computed((): UpNextItem[] => [
      ...userQueue.value.map((file, i) => ({
        kind: "queue" as const,
        file,
        queueIndex: i,
      })),
      ...sourceList.value.slice(currentIndex.value + 1).map((file, i) => ({
        kind: "source" as const,
        file,
        sourceIndex: currentIndex.value + 1 + i,
      })),
    ]);

    const hasActiveFile = computed(() => activeFile.value !== null);
    const isAudio = computed(() => activeFile.value?.mimeType.startsWith("audio/") ?? false);
    const isStrip = computed(() => playerMode.value === "strip");

    // Engine bridge
    const registerEngine = (controls: EngineControls) => {
      _engine = controls;
    };
    const unregisterEngine = () => {
      _engine = null;
    };

    // Playback controls
    const play = () => {
      queueEnded.value = false;
      _engine?.play();
    };

    const playNow = (files: MediaFileDto[]) => {
      if (!files.length) return;
      userQueue.value = [...files.slice(1), ...userQueue.value];
      activeFile.value = files[0];
    };

    const pause = () => _engine?.pause();
    const seek = (seconds: number) => _engine?.seek(seconds);

    const togglePlay = () => {
      if (!_engine) return;
      isPlaying.value ? _engine.pause() : _engine.play();
    };

    // Quality controls
    const selectVariant = (id: number | null) => _engine?.selectVariant(id);
    const setPlaybackRate = (rate: number) => _engine?.setPlaybackRate(rate);

    // Engine-only setters
    const setCurrentTime = (t: number) => {
      currentTime.value = t;
    };
    const setDuration = (d: number) => {
      duration.value = d;
    };
    const setIsPlaying = (v: boolean) => {
      isPlaying.value = v;
    };
    const setVariantTracks = (tracks: VariantTrack[]) => {
      variantTracks.value = tracks;
    };
    const setActiveVariantId = (id: number | null) => {
      activeVariantId.value = id;
    };
    const setAbrEnabled = (v: boolean) => {
      abrEnabled.value = v;
    };
    const setPlaybackRateState = (rate: number) => {
      playbackRate.value = rate;
    };
    const setPlayerMode = (mode: "expanded" | "pip" | "strip") => {
      playerMode.value = mode;
    };
    const clearActiveFile = () => {
      activeFile.value = null;
    };
    const setSnapCorner = (c: "tl" | "tr" | "bl" | "br") => {
      snapCorner.value = c;
    };
    const setVolume = (v: number) => {
      volume.value = Math.max(0, Math.min(1, v));
    };

    // Autoplay countdown
    const clearCountdown = () => {
      if (_countdownInterval !== null) {
        clearInterval(_countdownInterval);
        _countdownInterval = null;
      }
      if (_completionTimeout !== null) {
        clearTimeout(_completionTimeout);
        _completionTimeout = null;
      }
      autoplayCountdown.value = null;
    };

    const cancelAutoplay = () => {
      clearCountdown();
    };

    const startAutoplayCountdown = (seconds: number, onComplete: () => void) => {
      clearCountdown();
      autoplayCountdown.value = seconds;
      _countdownInterval = setInterval(() => {
        if (autoplayCountdown.value === null) {
          clearCountdown();
          return;
        }
        autoplayCountdown.value--;
        if (autoplayCountdown.value <= 0) {
          autoplayCountdown.value = 0;
          clearTimeout(_completionTimeout!);
          _completionTimeout = setTimeout(() => {
            clearCountdown();
            onComplete();
          }, 300);
          clearInterval(_countdownInterval!);
          _countdownInterval = null;
        }
      }, 1_000);
    };

    // Source management

    /**
     * Start playback from a new source snapshot.
     *
     * Pass `context` when the source is a paginated/infinite list so the store
     * can lazily fetch more pages as playback advances. Omit for bounded
     * sources (playlists, search results already fully loaded, etc.).
     */
    const setSource = (
      files: MediaFileDto[],
      id: string,
      startIndex: number,
      context?: PlaybackContext,
    ) => {
      if (sourceId.value !== id) userQueue.value = [];
      sourceList.value = files;
      sourceId.value = id;
      shuffled.value = false;
      originalSourceList.value = [];
      currentIndex.value = startIndex;
      activeFile.value = files[startIndex] ?? null;
      _playbackContext = context ?? null;
      loadedPage.value = context?.loadedPage ?? 0;
      totalPages.value = context?.totalPages ?? 0;
    };

    /**
     * Restores the runtime fetch context after navigation destroys the
     * originating component. Called by useStreamingMediaContext() in the layout.
     */
    const restoreContext = (fetchPage: PlaybackContext["fetchPage"]) => {
      if (!sourceId.value) return;
      _playbackContext = {
        fetchPage,
        loadedPage: loadedPage.value,
        totalPages: totalPages.value,
      };
    };

    const playFromSource = (index: number) => {
      currentIndex.value = index;
      activeFile.value = sourceList.value[index] ?? null;
    };

    // Lazy source expansion
    const _expandSource = async () => {
      if (!_playbackContext) return;
      if (_playbackContext.loadedPage >= _playbackContext.totalPages) return;
      if (isExpandingSource.value) return;

      isExpandingSource.value = true;
      try {
        const nextPage = _playbackContext.loadedPage + 1;
        const result = await _playbackContext.fetchPage(nextPage);
        _playbackContext.loadedPage = nextPage;
        _playbackContext.totalPages = result.totalPages;
        loadedPage.value = nextPage;
        totalPages.value = result.totalPages;

        const existingIds = new Set(sourceList.value.map((f) => f.fileId));
        const fresh = result.items.filter((f) => !existingIds.has(f.fileId));
        if (fresh.length) sourceList.value = [...sourceList.value, ...fresh];
      } finally {
        isExpandingSource.value = false;
      }
    };

    // Queue helpers
    const enqueue = (file: MediaFileDto) => {
      userQueue.value.push(file);
    };
    const dequeueAt = (index: number) => {
      userQueue.value.splice(index, 1);
    };
    const clearUserQueue = () => {
      userQueue.value = [];
    };

    const skipToQueueIndex = (index: number) => {
      const file = userQueue.value[index];
      if (!file) return;
      userQueue.value = userQueue.value.slice(index + 1);
      activeFile.value = file;
    };

    // Navigation
    const next = async () => {
      clearCountdown();

      if (repeatMode.value === "one") {
        seek(0);
        play();
        return;
      }

      // Drain user queue first
      if (userQueue.value.length > 0) {
        const file = userQueue.value.shift()!;
        const srcIdx = sourceList.value.findIndex((f) => f.fileId === file.fileId);
        if (srcIdx !== -1) currentIndex.value = srcIdx;
        activeFile.value = file;
        return;
      }

      // Pre-fetch when within 5 tracks of the loaded window edge
      const nearEnd =
        _playbackContext &&
        currentIndex.value >= sourceList.value.length - 5 &&
        _playbackContext.loadedPage < _playbackContext.totalPages;

      if (nearEnd) await _expandSource();

      if (currentIndex.value < sourceList.value.length - 1) {
        currentIndex.value++;
        activeFile.value = sourceList.value[currentIndex.value];
        return;
      }

      // We're at the end of the loaded window — try to expand before giving up
      if (_playbackContext && _playbackContext.loadedPage < _playbackContext.totalPages) {
        await _expandSource();
        if (currentIndex.value < sourceList.value.length - 1) {
          currentIndex.value++;
          activeFile.value = sourceList.value[currentIndex.value];
          return;
        }
      }

      if (repeatMode.value === "all") {
        currentIndex.value = 0;
        activeFile.value = sourceList.value[0];
      } else {
        queueEnded.value = true;
      }
    };

    const previous = () => {
      if (!hasPrevious.value) return;
      clearCountdown();
      currentIndex.value--;
      activeFile.value = sourceList.value[currentIndex.value];
    };

    const restartQueue = () => {
      queueEnded.value = false;
      currentIndex.value = 0;
      activeFile.value = sourceList.value[0];
      play();
    };

    const setCurrentIndex = (idx: number) => {
      currentIndex.value = idx;
      activeFile.value = sourceList.value[idx] ?? null;
    };

    const setActiveFile = (file: MediaFileDto) => {
      activeFile.value = file;
      const idx = sourceList.value.findIndex((f) => f.fileId === file.fileId);
      currentIndex.value = idx;
    };

    // Shuffle
    const toggleShuffle = () => {
      if (shuffled.value) {
        const currentFile = activeFile.value;
        sourceList.value = [...originalSourceList.value];
        originalSourceList.value = [];
        shuffled.value = false;
        if (currentFile) {
          currentIndex.value = sourceList.value.findIndex((f) => f.fileId === currentFile.fileId);
        }
      } else {
        originalSourceList.value = [...sourceList.value];
        const current = activeFile.value;
        const rest = sourceList.value.filter((f) => f.fileId !== current?.fileId);
        for (let i = rest.length - 1; i > 0; i--) {
          const j = Math.floor(Math.random() * (i + 1));
          [rest[i], rest[j]] = [rest[j], rest[i]];
        }
        sourceList.value = current ? [current, ...rest] : rest;
        currentIndex.value = current ? 0 : -1;
        shuffled.value = true;
        // Shuffle invalidates lazy context — reordered list is source of truth now
        _playbackContext = null;
      }
    };

    // Repeat
    const toggleLoop = () => {
      const cycle = { off: "all", all: "one", one: "off" } as const;
      repeatMode.value = cycle[repeatMode.value];
    };

    // Autoplay toggles
    const toggleAutoplay = () => {
      autoplay.value = !autoplay.value;
    };
    const toggleVideoAutoplay = () => {
      videoAutoplay.value = !videoAutoplay.value;
      if (!videoAutoplay.value) clearCountdown();
    };

    return {
      // Persisted
      activeFile,
      snapCorner,
      volume,
      currentIndex,
      autoplay,
      videoAutoplay,
      playerMode,
      activePlaylistId,
      repeatMode,
      shuffled,
      sourceList,
      sourceId,
      userQueue,
      loadedPage,
      totalPages,

      // Computed
      upNextItems,
      hasNext,
      hasPrevious,
      hasActiveFile,
      isAudio,
      isStrip,
      isExpandingSource,

      // Source management
      setSource,
      restoreContext,
      playFromSource,

      // Queue helpers
      playNow,
      enqueue,
      dequeueAt,
      clearUserQueue,
      skipToQueueIndex,

      // Runtime
      currentTime,
      duration,
      isPlaying,
      variantTracks,
      activeVariantId,
      abrEnabled,
      playbackRate,
      autoplayCountdown,

      // Engine bridge
      registerEngine,
      unregisterEngine,

      // Playback actions
      play,
      pause,
      seek,
      togglePlay,

      // Quality actions
      selectVariant,
      setPlaybackRate,

      // Engine-only setters
      setCurrentTime,
      setDuration,
      setIsPlaying,
      setVariantTracks,
      setActiveVariantId,
      setAbrEnabled,
      setPlaybackRateState,

      // Standard actions
      setPlayerMode,
      clearActiveFile,
      setSnapCorner,
      setVolume,
      toggleShuffle,
      queueEnded,
      restartQueue,
      setCurrentIndex,
      setActiveFile,
      toggleLoop,
      next,
      previous,
      toggleAutoplay,
      toggleVideoAutoplay,
      startAutoplayCountdown,
      cancelAutoplay,
    };
  },
  {
    persist: {
      pick: [
        "activeFile",
        "snapCorner",
        "volume",
        "currentIndex",
        "sourceId",
        "userQueue",
        "autoplay",
        "sourceList",
        "originalSourceList",
        "videoAutoplay",
        "playerMode",
        "repeatMode",
        "shuffled",
        "loadedPage",
        "totalPages",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
