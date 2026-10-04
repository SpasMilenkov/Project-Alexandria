import { ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import type {
  AfterContextEnds,
  ContextSnapshot,
  ContextState,
  MediaKind,
  NowPlaying,
  PlayerError,
  PlayerNotice,
  QueueEntry,
  RepeatMode,
  ShufflePreference,
} from "./types";

import { createTransport } from "./transport";

const NOTICE_TIMEOUT_MS = 8000;

export interface NoticeAction {
  label: string;
  run: () => void;
}

// oxlint-disable-next-line max-statements
export const createPlayerState = () => {
  const transport = createTransport();
  const nowPlaying = ref<NowPlaying | null>(null);
  const context = ref<ContextState | null>(null);
  const queueEntries = ref<QueueEntry[]>([]);
  const snapshot = ref<ContextSnapshot | null>(null);
  const heard = ref<Set<string>>(new Set());
  const shufflePreference = ref<ShufflePreference>({ audio: false, video: false });
  const afterContextEnds = ref<AfterContextEnds>("library");
  const repeatMode = ref<RepeatMode>("off");
  const videoAutoplay = ref(true);
  const playerMode = ref<"expanded" | "pip" | "strip">("pip");
  const snapCorner = ref<"tl" | "tr" | "bl" | "br">("br");
  const playerDismissed = ref(false);
  const lyricsOpen = ref(false);
  const transientEpoch = ref(0);
  const queueEnded = ref(false);
  const playbackOwnerId = ref<string | null>(null);
  const orderBusy = ref(false);
  const error = ref<PlayerError | null>(null);
  const notices = ref<PlayerNotice[]>([]);
  const engineBuffering = ref(false);
  const engineLoadError = ref<string | null>(null);
  const engineReady = ref(false);
  const lastContextFile = ref<MediaFileDto | null>(null);
  const restoredFor = ref<string | null>(null);
  const orderRevision = ref(0);
  const videoSurfaces = ref(0);
  let noticeSequence = 0;
  let noticeTimer: ReturnType<typeof setTimeout> | null = null;

  const mediaKind = (file: MediaFileDto | null): MediaKind => {
    if (!file) return "audio";
    if (file.mimeType.startsWith("audio/")) return "audio";
    return "video";
  };

  const markHeard = (file: MediaFileDto) => {
    if (heard.value.has(file.fileId)) return;
    heard.value = new Set(heard.value).add(file.fileId);
  };

  const isHeard = (fileId: string): boolean => heard.value.has(fileId);

  const startCycle = () => {
    heard.value = new Set();
  };

  const setError = (message: string, retry: () => Promise<void>) => {
    error.value = { message, retry };
  };

  const clearError = () => {
    error.value = null;
  };

  const dismissNotices = () => {
    notices.value = [];
    if (noticeTimer !== null) {
      clearTimeout(noticeTimer);
      noticeTimer = null;
    }
  };

  const showNotice = (message: string, action: NoticeAction | null = null) => {
    noticeSequence += 1;
    notices.value = [
      ...notices.value,
      {
        id: noticeSequence,
        message,
        actionLabel: action?.label ?? null,
        onAction: action?.run ?? null,
      },
    ];
    if (noticeTimer !== null) clearTimeout(noticeTimer);
    noticeTimer = setTimeout(dismissNotices, NOTICE_TIMEOUT_MS);
  };

  return {
    transport,
    nowPlaying,
    context,
    queueEntries,
    snapshot,
    heard,
    shufflePreference,
    afterContextEnds,
    repeatMode,
    videoAutoplay,
    playerMode,
    snapCorner,
    playerDismissed,
    lyricsOpen,
    transientEpoch,
    queueEnded,
    playbackOwnerId,
    orderBusy,
    error,
    notices,
    engineBuffering,
    engineLoadError,
    engineReady,
    lastContextFile,
    restoredFor,
    orderRevision,
    videoSurfaces,
    mediaKind,
    markHeard,
    isHeard,
    startCycle,
    setError,
    clearError,
    showNotice,
    dismissNotices,
  };
};

export type PlayerState = ReturnType<typeof createPlayerState>;
