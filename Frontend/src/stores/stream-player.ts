// oxlint-disable max-statements
// oxlint-disable max-lines-per-function
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import {
  SHUFFLE_BATCH_LIMIT,
  httpStatus,
  isAuthError,
  shuffleApi,
  type ShuffleSessionResponse,
} from "@/api/shuffle";
import {
  alignRangeStart,
  isRangeCovered,
  markRangeScanned,
  rangeKey,
  rangesToRetain,
  shouldPrefetchRange,
  SHUFFLE_RANGE_SIZE,
  type ScannedRange,
} from "@/utils/player-shuffle-buffer";
import {
  appendPlaylistToQueue,
  describeSource,
  fetchAnchorPage,
  fetchSequentialPage,
  sameSource,
  type SourceAnchor,
  type SourceDescriptor,
} from "@/utils/player-source";

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

type HistoryFlushFn = () => Promise<void>;

type UpNextItem =
  | { kind: "queue"; file: MediaFileDto; queueIndex: number }
  | { kind: "source"; file: MediaFileDto; sourceIndex: number };

/**
 * Describes how to lazily expand the playback source window.
 * Callers pass fetchPage + pageSize when starting playback from a
 * paginated sequential source.
 */
type Cursor = {
  fetchPage: (page: number) => Promise<{ items: MediaFileDto[]; totalPages: number }>;
  pageSize: number;
};

const MAX_WINDOW_PAGES = 3;
const NOTICE_TIMEOUT_MS = 8000;

let _engine: EngineControls | null = null;
let _cursor: Cursor | null = null;
let _historyFlush: HistoryFlushFn | null = null;

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
    const userQueue = ref<MediaFileDto[]>([]);
    const cursorPage = ref(1);
    const cursorOffset = ref(0);
    const totalPages = ref(0);

    // Persisted shuffle/source context (replaces the legacy sourceId + window flag)
    const playbackOwnerId = ref<string | null>(null);
    const sourceDescriptor = ref<SourceDescriptor | null>(null);
    const sourceAnchor = ref<SourceAnchor | null>(null);
    const activePlaybackOrigin = ref<"source" | "manual">("source");
    const shuffleSessionId = ref<string | null>(null);
    const shufflePosition = ref(-1);
    const shuffleTotalCount = ref(0);
    const shuffled = ref(false);

    // Runtime sequential window (not persisted)
    const sourceList = ref<MediaFileDto[]>([]);
    const currentIndex = ref(-1);
    const windowStartPage = ref(1);
    const windowEndPage = ref(1);

    // Runtime shuffle buffer (not persisted; the server owns the order)
    const shuffleEntries = ref(new Map<number, MediaFileDto>());
    const shuffleScanned = ref<ScannedRange[]>([]);
    const shuffleBusy = ref(false);
    const shuffleRestoring = ref(false);
    const shuffleLoadingMore = ref(false);
    const shuffleError = ref<string | null>(null);
    const shuffleNotice = ref<string | null>(null);
    const parkedContext = ref<{
      descriptor: SourceDescriptor;
      sessionId: string;
      position: number;
      totalCount: number;
    } | null>(null);
    const inflightRanges = new Map<string, Promise<void>>();
    const generation = ref(0);
    let navChain: Promise<void> = Promise.resolve();
    let endedOccurrence = 0;
    let handledEndedOccurrence = -1;
    let shuffleRetry: (() => Promise<void>) | null = null;
    let rebuiltSession = false;
    let noticeTimer: ReturnType<typeof setTimeout> | null = null;

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
    const sourceId = computed(() =>
      sourceDescriptor.value ? describeSource(sourceDescriptor.value) : null,
    );

    const isPlaylistSource = computed(() => sourceDescriptor.value?.playlistId !== null);

    const hasNext = computed(() => {
      if (userQueue.value.length > 0) return true;
      if (parkedContext.value) return true;
      if (shuffled.value && shuffleSessionId.value) {
        if (shufflePosition.value < shuffleTotalCount.value - 1) return true;
        return repeatMode.value !== "off" && shuffleTotalCount.value > 0;
      }
      return (
        isExpandingSource.value ||
        currentIndex.value < sourceList.value.length - 1 ||
        windowEndPage.value < totalPages.value ||
        (sourceList.value.length === 0 && cursorPage.value < totalPages.value) ||
        (repeatMode.value !== "off" && totalPages.value > 0)
      );
    });

    const hasPrevious = computed(() => {
      if (shuffled.value && shuffleSessionId.value) return shufflePosition.value > 0;
      return (
        currentIndex.value > 0 ||
        windowStartPage.value > 1 ||
        (sourceList.value.length === 0 && (cursorPage.value > 1 || cursorOffset.value > 0))
      );
    });

    const upNextItems = computed((): UpNextItem[] => {
      const queued = userQueue.value.map((file, i) => ({
        kind: "queue" as const,
        file,
        queueIndex: i,
      }));
      if (shuffled.value && shuffleSessionId.value) {
        const upcoming: UpNextItem[] = [];
        const positions = [...shuffleEntries.value.keys()]
          .filter((p) => p > shufflePosition.value)
          .sort((a, b) => a - b);
        for (const position of positions) {
          const file = shuffleEntries.value.get(position);
          if (file) upcoming.push({ kind: "source" as const, file, sourceIndex: position });
        }
        return [...queued, ...upcoming];
      }
      return [
        ...queued,
        ...sourceList.value.slice(currentIndex.value + 1).map((file, i) => ({
          kind: "source" as const,
          file,
          sourceIndex: currentIndex.value + 1 + i,
        })),
      ];
    });

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
    const registerHistoryFlush = (fn: HistoryFlushFn | null) => {
      _historyFlush = fn;
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
      activePlaybackOrigin.value = "manual";
      endedOccurrence++;
      handledEndedOccurrence = -1;
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
     * window position so the sequential window can be rebuilt after reload.
     */
    const updateCursor = () => {
      if (!_cursor) return;
      const pagesBeforeCurrent = Math.floor(currentIndex.value / _cursor.pageSize);
      cursorPage.value = windowStartPage.value + pagesBeforeCurrent;
      cursorOffset.value = currentIndex.value % _cursor.pageSize;
    };

    const bumpGeneration = () => {
      generation.value++;
      inflightRanges.clear();
    };

    const showNotice = (message: string) => {
      shuffleNotice.value = message;
      if (noticeTimer !== null) clearTimeout(noticeTimer);
      noticeTimer = setTimeout(() => {
        shuffleNotice.value = null;
        noticeTimer = null;
      }, NOTICE_TIMEOUT_MS);
    };

    const dismissShuffleNotice = () => {
      shuffleNotice.value = null;
      if (noticeTimer !== null) {
        clearTimeout(noticeTimer);
        noticeTimer = null;
      }
    };

    const clearShuffleState = () => {
      shuffleSessionId.value = null;
      shufflePosition.value = -1;
      shuffleTotalCount.value = 0;
      shuffled.value = false;
      shuffleEntries.value = new Map();
      shuffleScanned.value = [];
      shuffleError.value = null;
      shuffleRetry = null;
      inflightRanges.clear();
      rebuiltSession = false;
    };

    const releaseSession = async (sessionId: string | null) => {
      if (!sessionId) return;
      try {
        await shuffleApi.deleteSession(sessionId);
      } catch (err: unknown) {
        if (!isAuthError(err) && httpStatus(err) !== 404) throw err;
      }
    };

    const seedBuffer = (response: ShuffleSessionResponse) => {
      const entries = new Map<number, MediaFileDto>();
      for (const item of response.items) entries.set(item.position, item.file);
      shuffleEntries.value = entries;
      shuffleScanned.value = markRangeScanned(
        [],
        response.offset,
        response.offset + response.scannedCount,
      );
      enforceBufferLimit();
    };

    const enforceBufferLimit = () => {
      const { keep } = rangesToRetain(shuffleEntries.value, shufflePosition.value);
      if (keep.size >= new Set([...shuffleEntries.value.keys()].map(alignRangeStart)).size) {
        return;
      }
      const retained = new Map<number, MediaFileDto>();
      for (const [position, file] of shuffleEntries.value) {
        if (keep.has(alignRangeStart(position))) retained.set(position, file);
      }
      shuffleEntries.value = retained;
      shuffleScanned.value = [...keep]
        .sort((a, b) => a - b)
        .filter((start) =>
          isRangeCovered(
            shuffleScanned.value,
            start,
            Math.min(start + SHUFFLE_RANGE_SIZE, shuffleTotalCount.value),
          ),
        )
        .map((start) => ({
          from: start,
          to: Math.min(start + SHUFFLE_RANGE_SIZE, shuffleTotalCount.value),
        }));
    };

    const dropParkedContext = (release: boolean) => {
      const park = parkedContext.value;
      parkedContext.value = null;
      if (release && park) void releaseSession(park.sessionId).catch(() => undefined);
    };

    // Source management
    /**
     * Start playback from a new sequential source snapshot.
     * An active shuffle session is parked for later auto-resume instead of
     * released; manual queue entries always survive the switch.
     */
    const setSource = (
      pageItems: MediaFileDto[],
      descriptor: SourceDescriptor,
      pageNum: number,
      activeIndexInPage: number,
      ttlPages: number,
      fetchPage: Cursor["fetchPage"],
      anchor?: SourceAnchor,
    ) => {
      const previousSession = shuffleSessionId.value;
      const previousDescriptor = sourceDescriptor.value;
      const previousShuffled = shuffled.value;
      bumpGeneration();
      const displaced = parkedContext.value;
      parkedContext.value = null;
      if (
        previousShuffled &&
        previousSession &&
        previousDescriptor &&
        shuffleTotalCount.value > 0
      ) {
        parkedContext.value = {
          descriptor: { ...previousDescriptor },
          sessionId: previousSession,
          position: shufflePosition.value,
          totalCount: shuffleTotalCount.value,
        };
      }
      if (displaced && displaced.sessionId !== parkedContext.value?.sessionId) {
        void releaseSession(displaced.sessionId).catch(() => undefined);
      }
      sourceDescriptor.value = { ...descriptor };
      activePlaylistId.value = descriptor.playlistId;
      sourceList.value = [...pageItems];
      windowStartPage.value = pageNum;
      windowEndPage.value = pageNum;
      currentIndex.value = activeIndexInPage;
      cursorPage.value = pageNum;
      cursorOffset.value = Math.max(0, activeIndexInPage);
      totalPages.value = ttlPages;
      activeFile.value = pageItems[activeIndexInPage] ?? null;
      activePlaybackOrigin.value = "source";
      const anchorFile = pageItems[activeIndexInPage] ?? null;
      sourceAnchor.value = anchor ?? (anchorFile ? toAnchor(descriptor, anchorFile) : null);
      clearShuffleState();
      queueEnded.value = false;
      endedOccurrence++;
      _cursor = { fetchPage, pageSize: pageItems.length || 20 };
    };

    const toAnchor = (descriptor: SourceDescriptor, file: MediaFileDto): SourceAnchor => {
      if (descriptor.playlistId && file.playlistItemId) {
        return { fileId: file.fileId, playlistItemId: file.playlistItemId };
      }
      return { fileId: file.fileId };
    };

    const commitSequentialIndex = (index: number) => {
      currentIndex.value = index;
      const file = sourceList.value[index] ?? null;
      activeFile.value = file;
      activePlaybackOrigin.value = "source";
      if (file && sourceDescriptor.value)
        sourceAnchor.value = toAnchor(sourceDescriptor.value, file);
      endedOccurrence++;
      handledEndedOccurrence = -1;
      updateCursor();
    };

    const playFromSource = async (index: number) => {
      if (shuffled.value && shuffleSessionId.value) {
        await jumpToShufflePosition(index);
        return;
      }
      commitSequentialIndex(index);
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
        const identity = (file: MediaFileDto) => file.playlistItemId ?? file.fileId;
        const existing = new Set(sourceList.value.map(identity));
        const fresh = items.filter((f) => !existing.has(identity(f)));
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
        const identity = (file: MediaFileDto) => file.playlistItemId ?? file.fileId;
        const existing = new Set(sourceList.value.map(identity));
        const fresh = items.filter((f) => !existing.has(identity(f)));
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
      activePlaybackOrigin.value = "manual";
      endedOccurrence++;
      handledEndedOccurrence = -1;
    };

    // Shuffle buffer reads
    const fetchShuffleRange = async (start: number): Promise<void> => {
      const sessionId = shuffleSessionId.value;
      if (!sessionId) return;
      const key = rangeKey(start, sessionId);
      const pending = inflightRanges.get(key);
      if (pending) {
        await pending;
        return;
      }
      const task = (async () => {
        const gen = generation.value;
        const alignedStart = alignRangeStart(start);
        try {
          const response = await shuffleApi.getSession(
            sessionId,
            alignedStart,
            SHUFFLE_BATCH_LIMIT,
          );
          if (gen !== generation.value) return;
          if (response.sessionId !== shuffleSessionId.value) return;
          if (!sameSource(response.source, sourceDescriptor.value)) return;
          shuffleTotalCount.value = response.totalCount;
          const next = new Map(shuffleEntries.value);
          for (
            let position = response.offset;
            position < response.offset + response.scannedCount;
            position++
          ) {
            next.delete(position);
          }
          for (const item of response.items) {
            next.set(item.position, item.file);
          }
          shuffleEntries.value = next;
          shuffleScanned.value = markRangeScanned(
            shuffleScanned.value,
            response.offset,
            response.offset + response.scannedCount,
          );
          enforceBufferLimit();
        } catch (err: unknown) {
          if (gen !== generation.value) return;
          throw err;
        } finally {
          inflightRanges.delete(key);
        }
      })();
      inflightRanges.set(key, task);
      await task;
    };

    const ensureShufflePosition = async (position: number): Promise<MediaFileDto | null> => {
      const sessionId = shuffleSessionId.value;
      const total = shuffleTotalCount.value;
      if (!sessionId || position < 0 || position >= total) return null;
      const buffered = shuffleEntries.value.get(position);
      if (buffered) return buffered;
      const start = alignRangeStart(position);
      if (
        isRangeCovered(shuffleScanned.value, start, Math.min(start + SHUFFLE_RANGE_SIZE, total))
      ) {
        return null;
      }
      await fetchShuffleRange(start);
      return shuffleEntries.value.get(position) ?? null;
    };

    const loadMoreShuffle = async () => {
      if (!shuffled.value || !shuffleSessionId.value || shuffleLoadingMore.value) return;
      shuffleLoadingMore.value = true;
      try {
        const start = shufflePosition.value + 1;
        for (let s = start; s < shuffleTotalCount.value; s++) {
          if (shuffleEntries.value.has(s)) continue;
          const covered = shuffleScanned.value;
          await ensureShufflePosition(s);
          if (shuffleScanned.value !== covered) break;
        }
      } catch (err: unknown) {
        handleRangeError(err);
      } finally {
        shuffleLoadingMore.value = false;
      }
    };

    const prefetchAround = (position: number) => {
      const total = shuffleTotalCount.value;
      if (!shuffleSessionId.value || total === 0) return;
      const start = alignRangeStart(position);
      const nextStart = shouldPrefetchRange(position, start, shuffleScanned.value, total);
      if (nextStart === null) return;
      fetchShuffleRange(nextStart).catch(() => undefined);
    };

    const commitShufflePosition = (position: number, file: MediaFileDto) => {
      shufflePosition.value = position;
      activeFile.value = file;
      activePlaybackOrigin.value = "source";
      if (sourceDescriptor.value) sourceAnchor.value = toAnchor(sourceDescriptor.value, file);
      queueEnded.value = false;
      endedOccurrence++;
      handledEndedOccurrence = -1;
      enforceBufferLimit();
      prefetchAround(position);
    };

    const jumpToShufflePosition = async (position: number) => {
      if (!shuffleSessionId.value) return;
      if (position < 0 || position >= shuffleTotalCount.value) return;
      shuffleError.value = null;
      try {
        const file = await ensureShufflePosition(position);
        if (!file) {
          shuffleError.value = "That track is no longer available.";
          shuffleRetry = () => jumpToShufflePosition(position);
          return;
        }
        commitShufflePosition(position, file);
      } catch (err: unknown) {
        handleRangeError(err);
      }
    };

    const handleRangeError = (err: unknown) => {
      if (isAuthError(err)) return;
      if (httpStatus(err) === 404) {
        void rebuildShuffleSession().catch(() => undefined);
        return;
      }
      shuffleError.value = "Could not load more tracks. Check your connection and retry.";
    };

    const rebuildShuffleSession = async () => {
      const descriptor = sourceDescriptor.value;
      if (!descriptor || rebuiltSession) {
        shuffleError.value = "This source is no longer available.";
        return;
      }
      rebuiltSession = true;
      const gen = generation.value;
      const anchor = sourceAnchor.value;
      shuffleBusy.value = true;
      try {
        let response: ShuffleSessionResponse;
        try {
          response = await shuffleApi.createSession({
            requestId: shuffleApi.newRequestId(),
            source: { isVideo: descriptor.isVideo, playlistId: descriptor.playlistId },
            anchorFileId: anchor?.fileId ?? null,
            anchorPlaylistItemId: anchor?.playlistItemId ?? null,
            limit: SHUFFLE_BATCH_LIMIT,
          });
        } catch (err: unknown) {
          if (httpStatus(err) !== 409 || !anchor) throw err;
          response = await shuffleApi.createSession({
            requestId: shuffleApi.newRequestId(),
            source: { isVideo: descriptor.isVideo, playlistId: descriptor.playlistId },
            limit: SHUFFLE_BATCH_LIMIT,
          });
          shufflePosition.value = -1;
          activePlaybackOrigin.value = "manual";
        }
        if (gen !== generation.value) return;
        const previousSession = shuffleSessionId.value;
        shuffleSessionId.value = response.sessionId;
        shuffleTotalCount.value = response.totalCount;
        seedBuffer(response);
        if (response.anchorPosition !== null && response.anchorPosition !== undefined) {
          shufflePosition.value = response.anchorPosition;
        }
        shuffled.value = true;
        rebuiltSession = false;
        shuffleRetry = null;
        showNotice("Shuffle refreshed. The previous shuffle session is no longer available.");
        if (previousSession && previousSession !== response.sessionId) {
          await releaseSession(previousSession).catch(() => undefined);
        }
      } catch (err: unknown) {
        if (gen !== generation.value) return;
        rebuiltSession = false;
        if (!isAuthError(err)) {
          shuffleError.value =
            httpStatus(err) === 404
              ? "This source is no longer available."
              : "Could not refresh shuffle. Check your connection and retry.";
          shuffleRetry = rebuildShuffleSession;
        }
      } finally {
        shuffleBusy.value = false;
      }
    };

    const retryShuffle = async () => {
      shuffleError.value = null;
      const retry = shuffleRetry;
      if (retry) {
        shuffleRetry = null;
        await retry();
        return;
      }
      if (!shuffleSessionId.value) {
        await enableShuffle();
        return;
      }
      try {
        const file = await ensureShufflePosition(Math.max(0, shufflePosition.value + 1));
        if (file && shufflePosition.value >= 0) prefetchAround(shufflePosition.value);
      } catch (err: unknown) {
        handleRangeError(err);
      }
    };

    // History barrier
    const flushHistoryForCycle = async (): Promise<boolean> => {
      if (!_historyFlush) return true;
      try {
        await _historyFlush();
        return true;
      } catch (err: unknown) {
        if (isAuthError(err)) return false;
        shuffleError.value = "Could not save listening history. Retry when ready.";
        return false;
      }
    };

    // Shuffle transitions
    const enableShuffle = async () => {
      dropParkedContext(true);
      const gen = generation.value;
      let descriptor = sourceDescriptor.value;
      if (!descriptor && activeFile.value) {
        descriptor = {
          isVideo: !activeFile.value.mimeType.startsWith("audio/"),
          playlistId: null,
        };
      }
      if (!descriptor) return;
      let anchor = sourceAnchor.value ?? null;
      if (activePlaybackOrigin.value === "source" && activeFile.value) {
        anchor = toAnchor(descriptor, activeFile.value);
      }
      let anchorAccepted = anchor !== null;
      shuffleBusy.value = true;
      shuffleError.value = null;
      try {
        const historyOk = await flushHistoryForCycle();
        if (gen !== generation.value) return;
        if (!historyOk) {
          shuffleRetry = enableShuffle;
          return;
        }
        let response: ShuffleSessionResponse;
        try {
          response = await shuffleApi.createSession({
            requestId: shuffleApi.newRequestId(),
            source: { isVideo: descriptor.isVideo, playlistId: descriptor.playlistId },
            anchorFileId: anchor?.fileId ?? null,
            anchorPlaylistItemId: anchor?.playlistItemId ?? null,
            limit: SHUFFLE_BATCH_LIMIT,
          });
        } catch (err: unknown) {
          if (httpStatus(err) === 409 && anchor) {
            response = await shuffleApi.createSession({
              requestId: shuffleApi.newRequestId(),
              source: { isVideo: descriptor.isVideo, playlistId: descriptor.playlistId },
              limit: SHUFFLE_BATCH_LIMIT,
            });
            if (gen !== generation.value) return;
            activePlaybackOrigin.value = "manual";
            anchorAccepted = false;
          } else if (httpStatus(err) === 404) {
            shuffleError.value = "This source is no longer available.";
            return;
          } else if (isAuthError(err)) {
            return;
          } else {
            shuffleError.value = "Could not start shuffle. Check your connection and retry.";
            shuffleRetry = enableShuffle;
            return;
          }
        }
        if (gen !== generation.value) return;
        const previousSession = shuffleSessionId.value;
        sourceDescriptor.value = { ...descriptor };
        activePlaylistId.value = descriptor.playlistId;
        shuffleSessionId.value = response.sessionId;
        shuffleTotalCount.value = response.totalCount;
        seedBuffer(response);
        if (anchor && anchorAccepted) {
          shufflePosition.value = response.anchorPosition ?? 0;
          sourceAnchor.value = anchor;
        } else {
          shufflePosition.value = -1;
          activePlaybackOrigin.value = "manual";
        }
        rebuiltSession = false;
        shuffled.value = true;
        prefetchAround(Math.max(0, shufflePosition.value));
        if (previousSession && previousSession !== response.sessionId) {
          await releaseSession(previousSession).catch(() => undefined);
        }
      } finally {
        shuffleBusy.value = false;
      }
    };

    const disableShuffle = async () => {
      const gen = generation.value;
      const descriptor = sourceDescriptor.value;
      const anchor = sourceAnchor.value;
      if (!descriptor || !anchor) {
        clearShuffleState();
        dropParkedContext(true);
        shuffled.value = false;
        return;
      }
      shuffleBusy.value = true;
      try {
        const page = await fetchAnchorPageSafe(descriptor, anchor);
        if (gen !== generation.value) return;
        if (!page) return;
        const sessionToRelease = shuffleSessionId.value;
        const preservedFile = activeFile.value;
        const preservedOrigin = activePlaybackOrigin.value;
        bumpGeneration();
        sourceList.value = [...page.items];
        windowStartPage.value = page.currentPage;
        windowEndPage.value = page.currentPage;
        currentIndex.value = page.anchorIndex;
        cursorPage.value = page.currentPage;
        cursorOffset.value = Math.max(0, page.anchorIndex);
        totalPages.value = page.totalPages;
        _cursor = {
          fetchPage: (p) => fetchSequentialPageSafe(descriptor, p),
          pageSize: page.items.length || 20,
        };
        clearShuffleState();
        activeFile.value = preservedFile;
        activePlaybackOrigin.value = preservedOrigin;
        shuffled.value = false;
        dropParkedContext(true);
        if (sessionToRelease) await releaseSession(sessionToRelease).catch(() => undefined);
      } finally {
        shuffleBusy.value = false;
      }
    };

    const fetchSequentialPageSafe = async (descriptor: SourceDescriptor, page: number) =>
      fetchSequentialPage(descriptor, page);

    const fetchAnchorPageSafe = async (descriptor: SourceDescriptor, anchor: SourceAnchor) => {
      try {
        const page = await fetchAnchorPage(descriptor, anchor);
        const anchorIndex = page.items.findIndex((f) =>
          anchor.playlistItemId
            ? f.playlistItemId === anchor.playlistItemId
            : f.fileId === anchor.fileId,
        );
        if (anchorIndex === -1) throw new Error("anchor-absent");
        return { ...page, anchorIndex };
      } catch (err: unknown) {
        if (isAuthError(err)) return null;
        if (httpStatus(err) === 404) {
          await disableToFirstPage(descriptor);
          return null;
        }
        shuffleError.value = "Could not restore the normal order. Retry to try again.";
        shuffleRetry = disableShuffle;
        return null;
      }
    };

    const disableToFirstPage = async (descriptor: SourceDescriptor) => {
      const gen = generation.value;
      try {
        const page = await fetchSequentialPage(descriptor, 1);
        if (gen !== generation.value) return;
        const preserved = activeFile.value;
        const sessionToRelease = shuffleSessionId.value;
        bumpGeneration();
        sourceList.value = [...page.items];
        windowStartPage.value = 1;
        windowEndPage.value = 1;
        totalPages.value = page.totalPages;
        _cursor = {
          fetchPage: (p) => fetchSequentialPageSafe(descriptor, p),
          pageSize: page.items.length || 20,
        };
        clearShuffleState();
        currentIndex.value = -1;
        cursorOffset.value = 0;
        activeFile.value = preserved;
        activePlaybackOrigin.value = "manual";
        shuffled.value = false;
        dropParkedContext(true);
        showNotice(
          "The previous track is no longer in this source. Kept playing it from the queue.",
        );
        if (sessionToRelease) await releaseSession(sessionToRelease).catch(() => undefined);
      } catch (err: unknown) {
        if (!isAuthError(err)) {
          shuffleError.value = "Could not restore the normal order. Retry to try again.";
        }
      }
    };

    const toggleShuffle = async () => {
      if (shuffleBusy.value || shuffleRestoring.value) return;
      if (shuffled.value) {
        await disableShuffle();
        return;
      }
      await enableShuffle();
    };

    const maybeResumeParked = async (): Promise<boolean> => {
      const park = parkedContext.value;
      parkedContext.value = null;
      if (!park) return false;
      const gen = generation.value;
      try {
        const response = await shuffleApi.getSession(
          park.sessionId,
          Math.max(0, park.position),
          SHUFFLE_BATCH_LIMIT,
        );
        if (gen !== generation.value) {
          parkedContext.value = park;
          return false;
        }
        if (!sameSource(response.source, park.descriptor)) return false;
        shuffleSessionId.value = response.sessionId;
        shuffleTotalCount.value = response.totalCount;
        sourceDescriptor.value = { ...park.descriptor };
        activePlaylistId.value = park.descriptor.playlistId;
        seedBuffer(response);
        rebuiltSession = false;
        shuffled.value = true;
        shuffleRetry = null;
        const resumeFrom = Math.max(0, Math.min(park.position, response.totalCount - 1));
        shufflePosition.value = resumeFrom - 1;
        showNotice("Back to your previous shuffle.");
        await nextShuffled();
        return true;
      } catch (err: unknown) {
        if (gen !== generation.value) return false;
        if (isAuthError(err)) {
          parkedContext.value = park;
          return true;
        }
        if (httpStatus(err) === 404) {
          return resumeParkedFresh(park, gen);
        }
        parkedContext.value = park;
        shuffleError.value = "Could not resume the previous shuffle. Retry when ready.";
        shuffleRetry = () => maybeResumeParked().then(() => undefined);
        return true;
      }
    };

    const resumeParkedFresh = async (
      park: NonNullable<typeof parkedContext.value>,
      gen: number,
    ): Promise<boolean> => {
      try {
        const response = await shuffleApi.createSession({
          requestId: shuffleApi.newRequestId(),
          source: { isVideo: park.descriptor.isVideo, playlistId: park.descriptor.playlistId },
          limit: SHUFFLE_BATCH_LIMIT,
        });
        if (gen !== generation.value) {
          void releaseSession(response.sessionId).catch(() => undefined);
          return false;
        }
        shuffleSessionId.value = response.sessionId;
        shuffleTotalCount.value = response.totalCount;
        sourceDescriptor.value = { ...park.descriptor };
        activePlaylistId.value = park.descriptor.playlistId;
        seedBuffer(response);
        rebuiltSession = false;
        shuffled.value = true;
        shuffleRetry = null;
        shufflePosition.value = -1;
        showNotice("Previous shuffle expired. Started a fresh cycle.");
        await nextShuffled();
        return true;
      } catch (err: unknown) {
        if (gen !== generation.value) return false;
        if (!isAuthError(err)) {
          shuffleError.value = "Could not resume the previous shuffle. Retry when ready.";
          shuffleRetry = () => {
            const retryGen = generation.value;
            return resumeParkedFresh(park, retryGen).then(() => undefined);
          };
        }
        return true;
      }
    };

    const appendPlaylist = async (playlistId: string): Promise<number> => {
      const before = userQueue.value.length;
      const total = await appendPlaylistToQueue(playlistId, (file) => userQueue.value.push(file));
      if (!activeFile.value && userQueue.value.length > before) {
        const file = userQueue.value.shift()!;
        activeFile.value = file;
        activePlaybackOrigin.value = "manual";
        endedOccurrence++;
        handledEndedOccurrence = -1;
      }
      return total;
    };

    const repeatAllCycle = async (): Promise<boolean> => {
      const gen = generation.value;
      const descriptor = sourceDescriptor.value;
      if (!descriptor) return false;
      shuffleBusy.value = true;
      try {
        const historyOk = await flushHistoryForCycle();
        if (gen !== generation.value) return false;
        if (!historyOk) {
          shuffleRetry = async () => {
            await repeatAllCycle();
          };
          return false;
        }
        const avoid =
          (shufflePosition.value >= 0 ? shuffleEntries.value.get(shufflePosition.value) : null)
            ?.fileId ??
          sourceAnchor.value?.fileId ??
          null;
        let response: ShuffleSessionResponse;
        try {
          response = await shuffleApi.createSession({
            requestId: shuffleApi.newRequestId(),
            source: { isVideo: descriptor.isVideo, playlistId: descriptor.playlistId },
            avoidFirstFileId: avoid,
            limit: SHUFFLE_BATCH_LIMIT,
          });
        } catch (err: unknown) {
          if (!isAuthError(err)) {
            shuffleError.value = "Could not start a new cycle. Check your connection and retry.";
            shuffleRetry = async () => {
              await repeatAllCycle();
            };
          }
          return false;
        }
        if (gen !== generation.value) return false;
        if (response.totalCount === 0) {
          queueEnded.value = true;
          return true;
        }
        const previousSession = shuffleSessionId.value;
        shuffleSessionId.value = response.sessionId;
        shuffleTotalCount.value = response.totalCount;
        seedBuffer(response);
        rebuiltSession = false;
        shuffled.value = true;
        let firstPosition = 0;
        let first: MediaFileDto | null = null;
        while (firstPosition < response.totalCount && !first) {
          first = await ensureShufflePosition(firstPosition);
          if (!first) firstPosition++;
        }
        if (first) {
          commitShufflePosition(firstPosition, first);
          play();
        } else {
          queueEnded.value = true;
        }
        if (previousSession && previousSession !== response.sessionId) {
          await releaseSession(previousSession).catch(() => undefined);
        }
        return true;
      } finally {
        shuffleBusy.value = false;
      }
    };

    // Navigation
    const enqueueNavigation = (run: () => Promise<void>): Promise<void> => {
      const gen = generation.value;
      const op = navChain.then(async () => {
        if (gen !== generation.value) return;
        await run();
      });
      navChain = op.catch(() => undefined);
      return op;
    };

    const next = () => enqueueNavigation(nextInternal);

    const nextInternal = async () => {
      clearCountdown();

      if (repeatMode.value === "one") {
        seek(0);
        play();
        endedOccurrence++;
        handledEndedOccurrence = -1;
        return;
      }

      // Drain user queue first
      if (userQueue.value.length > 0) {
        const file = userQueue.value.shift()!;
        activeFile.value = file;
        activePlaybackOrigin.value = "manual";
        endedOccurrence++;
        handledEndedOccurrence = -1;
        return;
      }

      if (shuffled.value && shuffleSessionId.value) {
        await nextShuffled();
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
        commitSequentialIndex(currentIndex.value + 1);
        return;
      }

      // Still at the end — try one more expansion before giving up
      if (windowEndPage.value < totalPages.value) {
        const ok = await _expandForward();
        if (ok && currentIndex.value < sourceList.value.length - 1) {
          commitSequentialIndex(currentIndex.value + 1);
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
        commitSequentialIndex(0);
      } else if (!(await maybeResumeParked())) {
        queueEnded.value = true;
      }
    };

    const nextShuffled = async () => {
      let position = shufflePosition.value + 1;
      while (position < shuffleTotalCount.value) {
        let file: MediaFileDto | null = null;
        try {
          file = await ensureShufflePosition(position);
        } catch (err: unknown) {
          shuffleRetry = nextShuffled;
          handleRangeError(err);
          return;
        }
        if (file) {
          commitShufflePosition(position, file);
          play();
          return;
        }
        position++;
      }
      if (repeatMode.value === "all") {
        await repeatAllCycle();
        return;
      }
      if (await maybeResumeParked()) return;
      queueEnded.value = true;
    };

    const previous = () => enqueueNavigation(previousInternal);

    const previousInternal = async () => {
      clearCountdown();
      if (shuffled.value && shuffleSessionId.value) {
        await previousShuffled();
        return;
      }
      if (!hasPrevious.value) return;
      if (currentIndex.value > 0) {
        commitSequentialIndex(currentIndex.value - 1);
        return;
      }
      if (windowStartPage.value > 1) {
        const prevWindowIdx = currentIndex.value;
        const ok = await _expandBackward();
        // _expandBackward adjusts currentIndex to still point to the same file.
        // Step back one more to actually go to the previous track.
        if (ok && currentIndex.value > prevWindowIdx) {
          commitSequentialIndex(currentIndex.value - 1);
        }
      }
    };

    const previousShuffled = async () => {
      let position = shufflePosition.value - 1;
      while (position >= 0) {
        let file: MediaFileDto | null = null;
        try {
          file = await ensureShufflePosition(position);
        } catch (err: unknown) {
          shuffleRetry = previousShuffled;
          handleRangeError(err);
          return;
        }
        if (file) {
          commitShufflePosition(position, file);
          play();
          return;
        }
        position--;
      }
    };

    const handleTrackEnded = () => {
      if (handledEndedOccurrence === endedOccurrence) return;
      handledEndedOccurrence = endedOccurrence;
      void next();
    };

    const handleActiveFileUnavailable = async () => {
      if (!shuffled.value || shufflePosition.value < 0) return;
      const nextEntries = new Map(shuffleEntries.value);
      nextEntries.delete(shufflePosition.value);
      shuffleEntries.value = nextEntries;
      await next();
    };

    const restartQueue = async () => {
      queueEnded.value = false;
      if (shuffled.value && sourceDescriptor.value) {
        await repeatAllCycle();
        return;
      }
      commitSequentialIndex(0);
      play();
    };

    const setCurrentIndex = (idx: number) => {
      commitSequentialIndex(idx);
    };

    const setActiveFile = (file: MediaFileDto) => {
      activeFile.value = file;
      endedOccurrence++;
      handledEndedOccurrence = -1;
      if (shuffled.value && shuffleSessionId.value) {
        for (const [position, entry] of shuffleEntries.value) {
          if (entry.fileId === file.fileId) {
            shufflePosition.value = position;
            activePlaybackOrigin.value = "source";
            if (sourceDescriptor.value) sourceAnchor.value = toAnchor(sourceDescriptor.value, file);
            return;
          }
        }
        activePlaybackOrigin.value = "manual";
        return;
      }
      const idx = sourceList.value.findIndex((f) => f.fileId === file.fileId);
      if (idx !== -1) {
        currentIndex.value = idx;
        activePlaybackOrigin.value = "source";
        if (sourceDescriptor.value) sourceAnchor.value = toAnchor(sourceDescriptor.value, file);
        updateCursor();
      } else {
        activePlaybackOrigin.value = "manual";
      }
    };

    // Session restore + owner lifecycle
    const restoreSequentialContext = async () => {
      const gen = generation.value;
      const descriptor = sourceDescriptor.value;
      const preserved = activeFile.value;
      if (!descriptor || !preserved) return;
      const anchor = sourceAnchor.value ?? { fileId: preserved.fileId };
      const fetchPage = (page: number) =>
        fetchSequentialPage(descriptor, page).then((result) => ({
          items: result.items,
          totalPages: result.totalPages,
        }));
      try {
        const page = await fetchAnchorPage(descriptor, anchor);
        if (gen !== generation.value) return;
        const index = page.items.findIndex((f) =>
          anchor.playlistItemId
            ? f.playlistItemId === anchor.playlistItemId
            : f.fileId === anchor.fileId,
        );
        if (index !== -1) {
          sourceList.value = [...page.items];
          windowStartPage.value = page.currentPage;
          windowEndPage.value = page.currentPage;
          currentIndex.value = index;
          cursorPage.value = page.currentPage;
          cursorOffset.value = index;
          totalPages.value = page.totalPages;
          _cursor = { fetchPage, pageSize: page.items.length || 20 };
          return;
        }
        const first = await fetchSequentialPage(descriptor, 1);
        if (gen !== generation.value) return;
        sourceList.value = [...first.items];
        windowStartPage.value = 1;
        windowEndPage.value = 1;
        totalPages.value = first.totalPages;
        _cursor = { fetchPage, pageSize: first.items.length || 20 };
        activePlaybackOrigin.value = "manual";
        currentIndex.value = -1;
        cursorOffset.value = 0;
      } catch (err: unknown) {
        if (!isAuthError(err)) {
          shuffleError.value = "Could not restore playback. Retry when ready.";
          shuffleRetry = restoreSequentialContext;
        }
      }
    };

    const restorePlaybackContext = async (ownerId: string | null) => {
      if (playbackOwnerId.value && ownerId && playbackOwnerId.value !== ownerId) {
        clearOwnerState();
      }
      playbackOwnerId.value = ownerId;
      if (!ownerId) return;
      if (shuffleSessionId.value && !shuffled.value) {
        await restoreShuffleContext();
        return;
      }
      if (
        !shuffled.value &&
        sourceDescriptor.value &&
        activeFile.value &&
        sourceList.value.length === 0
      ) {
        await restoreSequentialContext();
      }
    };

    const restoreShuffleContext = async () => {
      const gen = generation.value;
      const sessionId = shuffleSessionId.value;
      const descriptor = sourceDescriptor.value;
      const position = shufflePosition.value;
      const total = shuffleTotalCount.value;
      if (!sessionId || !descriptor || total <= 0 || position < -1) {
        clearShuffleState();
        return;
      }
      shuffleRestoring.value = true;
      try {
        const response = await shuffleApi.getSession(
          sessionId,
          Math.max(0, position),
          SHUFFLE_BATCH_LIMIT,
        );
        if (gen !== generation.value) return;
        if (response.sessionId !== sessionId) return;
        if (!sameSource(response.source, descriptor)) {
          clearShuffleState();
          return;
        }
        shuffleTotalCount.value = response.totalCount;
        seedBuffer(response);
        shuffled.value = true;
        rebuiltSession = false;
        if (position >= 0) {
          const file = shuffleEntries.value.get(position);
          if (!file) {
            const found = await ensureShufflePosition(position);
            if (!found) {
              await rebuildShuffleSession();
              return;
            }
          }
        }
        prefetchAround(Math.max(0, position));
      } catch (err: unknown) {
        if (isAuthError(err)) return;
        if (httpStatus(err) === 404) {
          await rebuildShuffleSession();
          return;
        }
        shuffleError.value = "Could not reconnect the shuffle session. Retry when ready.";
        shuffleRetry = restoreShuffleContext;
      } finally {
        shuffleRestoring.value = false;
      }
    };

    const releaseShuffleSession = async () => {
      const sessionId = shuffleSessionId.value;
      if (!sessionId) return;
      await releaseSession(sessionId).catch(() => undefined);
    };

    const clearOwnerState = () => {
      bumpGeneration();
      clearActiveFile();
      userQueue.value = [];
      sourceDescriptor.value = null;
      activePlaylistId.value = null;
      sourceAnchor.value = null;
      activePlaybackOrigin.value = "source";
      clearShuffleState();
      dropParkedContext(true);
      queueEnded.value = false;
      playbackOwnerId.value = null;
      sourceList.value = [];
      currentIndex.value = -1;
      _cursor = null;
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
      userQueue,
      autoplay,
      videoAutoplay,
      playerMode,
      activePlaylistId,
      repeatMode,
      totalPages,

      // Persisted source/shuffle context
      playbackOwnerId,
      sourceDescriptor,
      sourceAnchor,
      activePlaybackOrigin,
      shuffleSessionId,
      shufflePosition,
      shuffleTotalCount,
      shuffled,

      // Runtime window
      sourceId,
      sourceList,
      currentIndex,
      windowStartPage,
      windowEndPage,
      isPlaylistSource,

      // Runtime shuffle
      shuffleEntries,
      shuffleScanned,
      shuffleBusy,
      shuffleRestoring,
      shuffleLoadingMore,
      shuffleError,
      shuffleNotice,
      parkedContext,

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
      playFromSource,
      appendPlaylist,
      loadMoreShuffle,

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
      registerHistoryFlush,

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
      handleTrackEnded,
      handleActiveFileUnavailable,
      toggleAutoplay,
      toggleVideoAutoplay,
      startAutoplayCountdown,
      cancelAutoplay,

      // Shuffle lifecycle
      retryShuffle,
      dismissShuffleNotice,
      restorePlaybackContext,
      releaseShuffleSession,
      clearOwnerState,
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
        "userQueue",
        "autoplay",
        "videoAutoplay",
        "playerMode",
        "repeatMode",
        "totalPages",
        "playbackOwnerId",
        "sourceDescriptor",
        "sourceAnchor",
        "activePlaybackOrigin",
        "shuffleSessionId",
        "shufflePosition",
        "shuffleTotalCount",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
