import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { shuffleApi } from "@/api/shuffle";
import type { MediaFileDto } from "@/api/streaming";
import { streamingApi } from "@/api/streaming";
import { usePlayerStore } from "@/stores/stream-player";

const file = (fileId: string): MediaFileDto => ({
  fileId,
  fileName: `${fileId}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: null,
  album: null,
  title: null,
  genre: null,
  year: null,
  transpilationJobId: "job",
  playlistItemId: null,
  isVideo: false,
  segmentPrefix: null,
});

const playlistRef = { isVideo: false, playlistId: "pl-1" };

beforeEach(() => {
  setActivePinia(createPinia());
  localStorage.clear();
  vi.spyOn(streamingApi, "getFilesForStreaming").mockImplementation(async (query: {
    page: number;
    pageSize: number;
  }) => ({
    items: [file("pf-1"), file("pf-2")],
    currentPage: query.page,
    pageSize: query.pageSize,
    totalCount: 2,
    totalPages: 1,
    hasPrevious: false,
    hasNext: false,
  }));
  vi.spyOn(shuffleApi, "createSession").mockImplementation(async (req: {
    requestId: string;
    source: { isVideo: boolean; playlistId: string | null };
  }) => ({
    sessionId: "session-1",
    source: req.source,
    algorithmVersion: 1,
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
    totalCount: 2,
    anchorPosition: null,
    offset: 0,
    scannedCount: 2,
    nextOffset: null,
    items: [
      { position: 0, file: file("pf-1") },
      { position: 1, file: file("pf-2") },
    ],
  }));
  vi.spyOn(shuffleApi, "getSession").mockImplementation(async (sessionId: string) => ({
    sessionId,
    source: { isVideo: false, playlistId: "pl-1" },
    algorithmVersion: 1,
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
    totalCount: 2,
    anchorPosition: null,
    offset: 0,
    scannedCount: 2,
    nextOffset: null,
    items: [
      { position: 0, file: file("pf-1") },
      { position: 1, file: file("pf-2") },
    ],
  }));
  vi.spyOn(shuffleApi, "deleteSession").mockResolvedValue(undefined);
});

describe("player chrome", () => {
  it("close hides the player but keeps the track and context", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    store.closePlayer();
    expect(store.playerDismissed).toBe(true);
    expect(store.nowPlaying?.file.fileId).toBe("pf-1");
    expect(store.context).not.toBeNull();
    store.resumePlayer();
    expect(store.playerDismissed).toBe(false);
  });

  it("toggles the shared lyrics panel", () => {
    const store = usePlayerStore();
    expect(store.lyricsOpen).toBe(false);
    store.toggleLyrics();
    expect(store.lyricsOpen).toBe(true);
    store.setLyricsOpen(false);
    expect(store.lyricsOpen).toBe(false);
  });

  it("bumps the transient epoch for surface coordination", () => {
    const store = usePlayerStore();
    expect(store.transientEpoch).toBe(0);
    store.closeTransientSurfaces();
    expect(store.transientEpoch).toBe(1);
  });

  it("clamps volume into range and routes it to the current engine", () => {
    const store = usePlayerStore();
    const audio = {
      play: vi.fn(),
      pause: vi.fn(),
      seek: vi.fn(),
      setVolume: vi.fn(),
      selectVariant: vi.fn(),
      setPlaybackRate: vi.fn(),
    };
    const dispose = store.registerEngine("audio", audio);
    store.setVolume(2);
    expect(store.volume).toBe(1);
    expect(audio.setVolume).toHaveBeenCalledWith(1);
    store.setVolume(-1);
    expect(store.volume).toBe(0);
    dispose();
  });

  it("routes transport to the engine of the current track kind", async () => {
    const store = usePlayerStore();
    const audio = {
      play: vi.fn(),
      pause: vi.fn(),
      seek: vi.fn(),
      setVolume: vi.fn(),
      selectVariant: vi.fn(),
      setPlaybackRate: vi.fn(),
    };
    const video = {
      play: vi.fn(),
      pause: vi.fn(),
      seek: vi.fn(),
      setVolume: vi.fn(),
      selectVariant: vi.fn(),
      setPlaybackRate: vi.fn(),
    };
    store.registerEngine("audio", audio);
    store.registerEngine("video", video);
    await store.startContext(playlistRef, { label: "P" });
    store.pause();
    expect(audio.pause).toHaveBeenCalledOnce();
    expect(video.pause).not.toHaveBeenCalled();
    const disposeVideo = store.registerEngine("video", video);
    disposeVideo();
    store.play();
    expect(audio.play).toHaveBeenCalledOnce();
    expect(video.play).not.toHaveBeenCalled();
  });

  it("owner teardown clears playback, lyrics, and dismissal", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    store.setLyricsOpen(true);
    store.closePlayer();
    store.clearOwnerState();
    expect(store.nowPlaying).toBeNull();
    expect(store.context).toBeNull();
    expect(store.lyricsOpen).toBe(false);
    expect(store.playerDismissed).toBe(false);
    expect(store.queueEntries).toEqual([]);
  });
});
