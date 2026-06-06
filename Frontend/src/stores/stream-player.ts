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
 * Describes how to lazily expand the playback source window.
 * Replaces the old PlaybackContext — callers pass fetchPage + pageSize
 * when starting playback from a paginated source.
 */
type Cursor = {
  fetchPage: (page: number) => Promise<{ items: MediaFileDto[]; totalPages: number }>;
  pageSize: number;
};

const MAX_WINDOW_PAGES = 3;

let _engine: EngineControls | null = null;
let _cursor: Cursor | null = null;

export const usePlayerStore = defineStore(
  "player",
  () => {
    // Persisted state
    const activeFile = ref<MediaFileDto | null>(null);
    const snapCorner = ref<"tl" | "tr" | "bl" | "br">("br");
    const volume = ref(0.5);
    const queueEnded = ref(false);
    const autoplay = ref(true);
    const videoAutoplay = ref(true);
    const playerMode = ref<"expanded" | "pip" | "strip">("pip");
    const activePlaylistId = ref<string | null>(null);
    const repeatMode = ref<"off" | "all" | "one">("off");
    const shuffled = ref(false);
    const sourceId = ref<string | null>(null);
    const userQueue = ref<MediaFileDto[]>([]);
    const totalPages = ref(0);
    const cursorPage = ref(1); // page number that contains activeFile
    const cursorOffset = ref(0); // index within that page

    // Runtime window (not persisted)
    const sourceList = ref<MediaFileDto[]>([]);
    const originalSourceList = ref<MediaFileDto[]>([]);
    const currentIndex = ref(-1);
    const windowStartPage = ref(1);
    const windowEndPage = ref(1);

    // Runtime playback state
    const currentTime = ref(0);
    const duration = ref(0);
    const isPlaying = ref(false);

    // Runtime quality state
    const variantTracks = ref<VariantTrack[]>([]);
    const activeVariantId = ref<number | null>(null);
    const abrEnabled = ref(true);
    const playbackRate = ref(1);

    // Runtime autoplay countdown (video only)
    const autoplayCountdown = ref<number | null>(null);
    let _countdownInterval: ReturnType<typeof setInterval> | null = null;
    let _completionTimeout: ReturnType<typeof setTimeout> | null = null;

    const isExpandingSource = ref(false);

    // Computed
    const hasNext = computed(
      () =>
        userQueue.value.length > 0 ||
        isExpandingSource.value ||
        currentIndex.value < sourceList.value.length - 1 ||
        windowEndPage.value < totalPages.value ||
        (sourceList.value.length === 0 && cursorPage.value < totalPages.value) ||
        (repeatMode.value !== "off" && totalPages.value > 0),
    );

    const hasPrevious = computed(
      () =>
        currentIndex.value > 0 ||
        windowStartPage.value > 1 ||
        (sourceList.value.length === 0 && (cursorPage.value > 1 || cursorOffset.value > 0)),
    );

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
      console.log(userQueue.value)
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

    // Cursor helper
    /**
     * Keeps the persisted cursorPage/cursorOffset in sync with the current
     * window position so we can rebuild the window after a page reload.
     */
    const updateCursor = () => {
      if (!_cursor) return;
      const pagesBeforeCurrent = Math.floor(currentIndex.value / _cursor.pageSize);
      cursorPage.value = windowStartPage.value + pagesBeforeCurrent;
      cursorOffset.value = currentIndex.value % _cursor.pageSize;
    };

    // Source management
    /**
     * Start playback from a new source snapshot.
     *
     * Callers supply the already-loaded page slice, the page number it came
     * from, and a fetchPage callback so the store can slide the window
     * forward/backward without re-fetching the initial page.
     */
    const setSource = (
      pageItems: MediaFileDto[],
      id: string,
      pageNum: number,
      activeIndexInPage: number,
      ttlPages: number,
      fetchPage: Cursor["fetchPage"],
    ) => {
      if (sourceId.value !== id) userQueue.value = [];
      sourceId.value = id;
      sourceList.value = [...pageItems];
      originalSourceList.value = [];
      shuffled.value = false;
      windowStartPage.value = pageNum;
      windowEndPage.value = pageNum;
      currentIndex.value = activeIndexInPage;
      cursorPage.value = pageNum;
      cursorOffset.value = activeIndexInPage;
      totalPages.value = ttlPages;
      activeFile.value = pageItems[activeIndexInPage] ?? null;
      _cursor = { fetchPage, pageSize: pageItems.length || 20 };
    };

    /**
     * Restores the runtime fetch context after navigation destroys the
     * originating component. Called by useStreamingMediaContext() in the layout.
     * Now async — callers should await it.
     */
    const restoreContext = async (fetchPage: Cursor["fetchPage"], pageSize = 20) => {
      if (!sourceId.value) return;
      _cursor = { fetchPage, pageSize };
      if (sourceList.value.length > 0) return; // window already populated
      isExpandingSource.value = true;
      try {
        const { items, totalPages: tp } = await fetchPage(cursorPage.value);
        totalPages.value = tp;
        sourceList.value = items;
        windowStartPage.value = cursorPage.value;
        windowEndPage.value = cursorPage.value;
        const safeOffset = Math.min(cursorOffset.value, items.length - 1);
        currentIndex.value = Math.max(0, safeOffset);
        // If the file at the cursor slot changed (upload/delete), find it by id
        if (items[currentIndex.value]?.fileId !== activeFile.value?.fileId) {
          const match = items.findIndex((f) => f.fileId === activeFile.value?.fileId);
          if (match !== -1) currentIndex.value = match;
        }
      } finally {
        isExpandingSource.value = false;
      }
    };

    const playFromSource = (index: number) => {
      currentIndex.value = index;
      activeFile.value = sourceList.value[index] ?? null;
      updateCursor();
    };

    // Lazy window expansion
    const _expandForward = async (): Promise<boolean> => {
      if (!_cursor || isExpandingSource.value) return false;
      if (windowEndPage.value >= totalPages.value) return false;
      isExpandingSource.value = true;
      try {
        const nextPage = windowEndPage.value + 1;
        const { items, totalPages: tp } = await _cursor.fetchPage(nextPage);
        totalPages.value = tp;
        const existing = new Set(sourceList.value.map((f) => f.fileId));
        const fresh = items.filter((f) => !existing.has(f.fileId));
        if (!fresh.length) {
          windowEndPage.value = nextPage;
          return false;
        }
        sourceList.value = [...sourceList.value, ...fresh];
        windowEndPage.value = nextPage;
        // Trim the front when the window exceeds MAX_WINDOW_PAGES
        const maxItems = MAX_WINDOW_PAGES * _cursor.pageSize;
        if (sourceList.value.length > maxItems && windowStartPage.value < windowEndPage.value) {
          const trim = sourceList.value.length - maxItems;
          sourceList.value = sourceList.value.slice(trim);
          windowStartPage.value++;
          currentIndex.value = Math.max(0, currentIndex.value - trim);
        }
        return true;
      } finally {
        isExpandingSource.value = false;
      }
    };

    const _expandBackward = async (): Promise<boolean> => {
      if (!_cursor || isExpandingSource.value) return false;
      if (windowStartPage.value <= 1) return false;
      isExpandingSource.value = true;
      try {
        const prevPage = windowStartPage.value - 1;
        const { items, totalPages: tp } = await _cursor.fetchPage(prevPage);
        totalPages.value = tp;
        const existing = new Set(sourceList.value.map((f) => f.fileId));
        const fresh = items.filter((f) => !existing.has(f.fileId));
        if (!fresh.length) {
          windowStartPage.value = prevPage;
          return false;
        }
        sourceList.value = [...fresh, ...sourceList.value];
        windowStartPage.value = prevPage;
        currentIndex.value += fresh.length; // still points to the same file
        // Trim the back
        const maxItems = MAX_WINDOW_PAGES * _cursor.pageSize;
        if (sourceList.value.length > maxItems) {
          sourceList.value = sourceList.value.slice(0, maxItems);
          windowEndPage.value--;
        }
        return true;
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
      const srcIdx = sourceList.value.findIndex((f) => f.fileId === file.fileId);
      if (srcIdx !== -1) {
        currentIndex.value = srcIdx;
        updateCursor();
      }
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
        updateCursor();
        return;
      }

      // Prefetch when within 5 tracks of the window's trailing edge
      if (
        currentIndex.value >= sourceList.value.length - 5 &&
        windowEndPage.value < totalPages.value
      ) {
        await _expandForward();
      }

      if (currentIndex.value < sourceList.value.length - 1) {
        currentIndex.value++;
        activeFile.value = sourceList.value[currentIndex.value];
        updateCursor();
        return;
      }

      // Still at the end — try one more expansion before giving up
      if (windowEndPage.value < totalPages.value) {
        const ok = await _expandForward();
        if (ok && currentIndex.value < sourceList.value.length - 1) {
          currentIndex.value++;
          activeFile.value = sourceList.value[currentIndex.value];
          updateCursor();
          return;
        }
      }

      if (repeatMode.value === "all") {
        if (_cursor) {
          isExpandingSource.value = true;
          try {
            const { items, totalPages: tp } = await _cursor.fetchPage(1);
            totalPages.value = tp;
            sourceList.value = items;
            windowStartPage.value = 1;
            windowEndPage.value = 1;
          } finally {
            isExpandingSource.value = false;
          }
        }
        currentIndex.value = 0;
        activeFile.value = sourceList.value[0] ?? null;
        updateCursor();
      } else {
        queueEnded.value = true;
      }
    };

    const previous = async () => {
      if (!hasPrevious.value) return;
      clearCountdown();

      if (currentIndex.value > 0) {
        currentIndex.value--;
        activeFile.value = sourceList.value[currentIndex.value];
        updateCursor();
        return;
      }

      if (windowStartPage.value > 1) {
        const prevWindowIdx = currentIndex.value;
        const ok = await _expandBackward();
        // _expandBackward adjusts currentIndex to still point to the same file.
        // Step back one more to actually go to the previous track.
        if (ok && currentIndex.value > prevWindowIdx) {
          currentIndex.value--;
          activeFile.value = sourceList.value[currentIndex.value];
          updateCursor();
        }
      }
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
      updateCursor();
    };

    const setActiveFile = (file: MediaFileDto) => {
      activeFile.value = file;
      const idx = sourceList.value.findIndex((f) => f.fileId === file.fileId);
      if (idx !== -1) {
        currentIndex.value = idx;
        updateCursor();
      }
    };

    // Shuffle
    const toggleShuffle = () => {
      if (shuffled.value) {
        const currentFile = activeFile.value;
        sourceList.value = [...originalSourceList.value];
        originalSourceList.value = [];
        shuffled.value = false;
        windowStartPage.value = 1;
        windowEndPage.value = 1;
        if (currentFile) {
          currentIndex.value = sourceList.value.findIndex((f) => f.fileId === currentFile.fileId);
          updateCursor();
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
        // Shuffle invalidates lazy fetch — window is the source of truth now
        _cursor = null;
        updateCursor();
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
      cursorPage,
      cursorOffset,
      sourceId,
      userQueue,
      autoplay,
      videoAutoplay,
      playerMode,
      activePlaylistId,
      repeatMode,
      shuffled,
      totalPages,

      // Runtime window
      sourceList,
      currentIndex,
      windowStartPage,
      windowEndPage,

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
        "cursorPage",
        "cursorOffset",
        "sourceId",
        "userQueue",
        "autoplay",
        "videoAutoplay",
        "playerMode",
        "repeatMode",
        "shuffled",
        "totalPages",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
