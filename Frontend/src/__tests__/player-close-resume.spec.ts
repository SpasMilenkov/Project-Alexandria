import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { MediaFileDto } from "@/api/streaming";

import { shuffleApi } from "@/api/shuffle";
import { usePlayerStore } from "@/stores/stream-player";

vi.mock("@/api/shuffle", () => ({
  SHUFFLE_BATCH_LIMIT: 50,
  httpStatus: () => null,
  isAuthError: () => false,
  shuffleApi: {
    newRequestId: vi.fn(() => "request"),
    createSession: vi.fn(),
    getSession: vi.fn(),
    deleteSession: vi.fn(),
  },
}));

const file = (id: string): MediaFileDto => ({
  fileId: id,
  fileName: `${id}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: `version-${id}`,
  duration: 180,
  artist: null,
  album: null,
  title: id,
  genre: null,
  year: null,
  transpilationJobId: `job-${id}`,
  playlistItemId: null,
  isVideo: false,
  segmentPrefix: null,
});

const engine = () => ({
  play: vi.fn(),
  pause: vi.fn(),
  seek: vi.fn(),
  setVolume: vi.fn(),
  selectVariant: vi.fn(),
  setPlaybackRate: vi.fn(),
});

const startAudio = (id = "track") => {
  const store = usePlayerStore();
  const items = [file(id)];
  store.setSource(items, { isVideo: false, playlistId: null }, 1, 0, 1, async () => ({
    items,
    totalPages: 1,
  }));
  return store;
};

describe("player close and resume", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("close stops playback but keeps track, queue, and source context", () => {
    const store = startAudio();
    const controls = engine();
    store.registerEngine(controls);
    store.enqueue(file("queued"));

    store.closePlayer();

    expect(controls.pause).toHaveBeenCalledOnce();
    expect(store.playerDismissed).toBe(true);
    expect(store.activeFile?.fileId).toBe("track");
    expect(store.userQueue).toHaveLength(1);
    expect(store.sourceDescriptor).toMatchObject({ isVideo: false });
    store.unregisterEngine();
  });

  it("resume reopens and continues playback", () => {
    const store = startAudio();
    const controls = engine();
    store.registerEngine(controls);

    store.closePlayer();
    store.resumePlayer();

    expect(store.playerDismissed).toBe(false);
    expect(controls.play).toHaveBeenCalledOnce();
    store.unregisterEngine();
  });

  it("starting another song reopens a dismissed player", () => {
    const store = startAudio();
    store.closePlayer();
    expect(store.playerDismissed).toBe(true);

    store.playNow([file("chosen")]);

    expect(store.playerDismissed).toBe(false);
    expect(store.activeFile?.fileId).toBe("chosen");
  });

  it("bridge transport delegates to the current engine", () => {
    const store = startAudio();
    const controls = engine();
    store.registerEngine(controls);

    const seekTo = 42;
    store.seek(seekTo);
    expect(controls.seek).toHaveBeenCalledWith(seekTo);

    store.setVolume(0.7);
    expect(controls.setVolume).toHaveBeenCalledWith(0.7);
    expect(store.volume).toBe(0.7);

    store.togglePlay();
    expect(controls.play).toHaveBeenCalledOnce();
    store.unregisterEngine();
  });

  it("setVolume clamps into range", () => {
    const store = startAudio();
    const controls = engine();
    store.registerEngine(controls);

    store.setVolume(2);
    expect(store.volume).toBe(1);
    expect(controls.setVolume).toHaveBeenCalledWith(1);

    store.setVolume(-1);
    expect(store.volume).toBe(0);
    expect(controls.setVolume).toHaveBeenCalledWith(0);
    store.unregisterEngine();
  });
});

describe("player engine ownership and shared state", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("a stale engine cannot unregister a newer engine", () => {
    const store = usePlayerStore();
    const first = engine();
    const second = engine();
    const firstToken = store.registerEngine(first);
    const secondToken = store.registerEngine(second);
    expect(firstToken).not.toBe(secondToken);

    store.unregisterEngine(firstToken);
    store.togglePlay();

    expect(second.play).toHaveBeenCalledOnce();
    expect(first.play).not.toHaveBeenCalled();
    store.unregisterEngine(secondToken);
  });

  it("a stale history-flush clear keeps the newer callback", async () => {
    const store = startAudio();
    const firstFlush = vi.fn(() => undefined);
    const secondFlush = vi.fn(() => undefined);
    const firstToken = store.registerHistoryFlush(firstFlush);
    store.registerHistoryFlush(secondFlush);
    store.registerHistoryFlush(null, firstToken);

    vi.mocked(shuffleApi.createSession).mockResolvedValueOnce({
      sessionId: "sess-1",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: new Date().toISOString(),
      expiresAt: new Date().toISOString(),
      totalCount: 1,
      anchorPosition: 0,
      offset: 0,
      scannedCount: 1,
      nextOffset: null,
      items: [{ position: 0, file: file("track") }],
    });
    await store.toggleShuffle();

    expect(secondFlush).toHaveBeenCalledOnce();
    expect(firstFlush).not.toHaveBeenCalled();
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
    const before = store.transientEpoch;

    store.closeTransientSurfaces();

    expect(store.transientEpoch).toBe(before + 1);
  });

  it("owner teardown clears playback, lyrics, and dismissal", () => {
    const store = startAudio();
    store.setLyricsOpen(true);
    store.closePlayer();

    store.clearOwnerState();

    expect(store.activeFile).toBeNull();
    expect(store.lyricsOpen).toBe(false);
    expect(store.playerDismissed).toBe(false);
    expect(store.userQueue).toHaveLength(0);
  });
});
