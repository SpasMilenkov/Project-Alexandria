import type { MediaFileDto } from "@/api/streaming";
import { LIBRARY_PAGE_SIZE } from "@/utils/player-source";

import { releaseShuffleSession } from "./order/shuffle";
import type { PlayerState } from "./state";
import type { AfterContextEnds } from "./types";

interface LegacyPersisted {
  activeFile?: MediaFileDto | null;
  snapCorner?: unknown;
  volume?: unknown;
  userQueue?: unknown;
  autoplay?: unknown;
  videoAutoplay?: unknown;
  playerMode?: unknown;
  playerDismissed?: unknown;
  repeatMode?: unknown;
  playbackOwnerId?: unknown;
  sourceDescriptor?: { isVideo?: unknown; playlistId?: unknown } | null;
  sourceAnchor?: { fileId?: unknown; playlistItemId?: unknown } | null;
  activePlaybackOrigin?: unknown;
  shuffleSessionId?: unknown;
  shufflePosition?: unknown;
  shuffleTotalCount?: unknown;
  cursorPage?: unknown;
  cursorOffset?: unknown;
  totalPages?: unknown;
  parkedContext?: { sessionId?: unknown } | null;
}

const asNumber = (value: unknown): number | null => {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  return null;
};

const asBoolean = (value: unknown): value is boolean => typeof value === "boolean";


const isFile = (value: unknown): value is MediaFileDto =>
  typeof value === "object" && value !== null && typeof (value as MediaFileDto).fileId === "string";

// oxlint-disable-next-line max-statements
export const migrateLegacyPlayer = (state: PlayerState, restamp: () => number) => {
  let raw: string | null = null;
  try {
    if (localStorage.getItem("player-v2")) return;
    raw = localStorage.getItem("player");
  } catch {
    return;
  }
  if (!raw) return;
  let legacy: LegacyPersisted | null = null;
  try {
    legacy = JSON.parse(raw) as LegacyPersisted;
  } catch {
    return;
  }
  if (!legacy || typeof legacy !== "object") return;

  const parkedSession =
    legacy.parkedContext && typeof legacy.parkedContext.sessionId === "string"
      ? legacy.parkedContext.sessionId
      : null;
  if (parkedSession) void releaseShuffleSession(parkedSession).catch(() => undefined);

  const volume = asNumber(legacy.volume);
  if (volume !== null) state.transport.volume.value = Math.max(0, Math.min(1, volume));
  if (legacy.snapCorner === "tl" || legacy.snapCorner === "tr" || legacy.snapCorner === "bl" || legacy.snapCorner === "br") {
    state.snapCorner.value = legacy.snapCorner;
  }
  if (legacy.playerMode === "expanded" || legacy.playerMode === "pip" || legacy.playerMode === "strip") {
    state.playerMode.value = legacy.playerMode;
  }
  if (legacy.repeatMode === "off" || legacy.repeatMode === "all" || legacy.repeatMode === "one") {
    state.repeatMode.value = legacy.repeatMode;
  }
  if (asBoolean(legacy.videoAutoplay)) state.videoAutoplay.value = legacy.videoAutoplay;
  if (asBoolean(legacy.playerDismissed)) state.playerDismissed.value = legacy.playerDismissed;
  if (typeof legacy.playbackOwnerId === "string" || legacy.playbackOwnerId === null) {
    state.playbackOwnerId.value = legacy.playbackOwnerId ?? null;
  }

  const after: AfterContextEnds = legacy.autoplay === false ? "stop" : "library";
  state.afterContextEnds.value = after;

  if (Array.isArray(legacy.userQueue)) {
    const entries = legacy.userQueue.filter(isFile).map((file, index) => ({
      id: `q-legacy-${index}`,
      file,
      group: null,
    }));
    state.queueEntries.value = entries;
  }

  const descriptor = legacy.sourceDescriptor;
  const isVideo = descriptor?.isVideo === true;
  const playlistId = typeof descriptor?.playlistId === "string" ? descriptor.playlistId : null;
  const sessionId = typeof legacy.shuffleSessionId === "string" ? legacy.shuffleSessionId : null;
  const shuffleTotal = asNumber(legacy.shuffleTotalCount) ?? 0;
  const shuffled = sessionId !== null && shuffleTotal > 0;
  const kind = isVideo ? "video" : "audio";
  state.shufflePreference.value = { audio: false, video: false, [kind]: shuffled };

  if (isFile(legacy.activeFile)) {
    state.nowPlaying.value = {
      instanceId: restamp(),
      file: legacy.activeFile,
      origin: legacy.activePlaybackOrigin === "source" ? "context" : "interrupt",
      ended: false,
      restored: true,
    };
  }

  if (descriptor && (playlistId !== null || descriptor.playlistId === null)) {
    const anchor = legacy.sourceAnchor;
    const fileId = typeof anchor?.fileId === "string" ? anchor.fileId : null;
    const itemId = typeof anchor?.playlistItemId === "string" ? anchor.playlistItemId : null;
    const shufflePosition = asNumber(legacy.shufflePosition) ?? -1;
    const cursorPage = asNumber(legacy.cursorPage) ?? 1;
    const cursorOffset = asNumber(legacy.cursorOffset) ?? 0;
    const totalPages = asNumber(legacy.totalPages) ?? 0;
    state.context.value = {
      ref: { isVideo, playlistId },
      label: playlistId ? "Playlist" : isVideo ? "Video library" : "Music library",
      shuffled,
      anchor: fileId ? { fileId, playlistItemId: itemId } : null,
      cursor: shuffled ? shufflePosition : (cursorPage - 1) * LIBRARY_PAGE_SIZE + cursorOffset,
      sessionId,
      expiresAt: null,
      total: shuffled ? shuffleTotal : totalPages * LIBRARY_PAGE_SIZE,
    };
  }
};
