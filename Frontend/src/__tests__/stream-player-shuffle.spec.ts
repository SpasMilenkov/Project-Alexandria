import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { ShuffleSessionResponse } from "@/api/shuffle";
import type { MediaFileDto } from "@/api/streaming";

import { shuffleApi } from "@/api/shuffle";
import { streamingApi } from "@/api/streaming";
import { usePlayerStore } from "@/stores/stream-player";

vi.mock("@/api/shuffle", () => ({
  SHUFFLE_BATCH_LIMIT: 50,
  httpStatus: (err: unknown) =>
    (err as { response?: { status?: number } })?.response?.status ?? null,
  isAuthError: () => false,
  shuffleApi: {
    newRequestId: vi.fn(() => "req-1"),
    createSession: vi.fn(),
    getSession: vi.fn(),
    deleteSession: vi.fn(),
  },
}));

vi.mock("@/api/streaming", () => ({
  streamingApi: { getFilesForStreaming: vi.fn() },
}));

const makeFile = (fileId: string, itemId?: string): MediaFileDto => ({
  fileId,
  fileName: `${fileId}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: "Artist",
  album: null,
  title: fileId,
  genre: null,
  year: null,
  transpilationJobId: `job-${fileId}`,
  playlistItemId: itemId ?? null,
  isVideo: false,
  segmentPrefix: null,
});

const makeSession = (
  sessionId: string,
  ids: string[],
  overrides: Partial<ShuffleSessionResponse> = {},
): ShuffleSessionResponse => ({
  sessionId,
  source: { isVideo: false, playlistId: null },
  algorithmVersion: 1,
  createdAt: new Date().toISOString(),
  expiresAt: new Date().toISOString(),
  totalCount: ids.length,
  anchorPosition: null,
  offset: 0,
  scannedCount: ids.length,
  nextOffset: null,
  items: ids.map((fileId, position) => ({ position, file: makeFile(fileId) })),
  ...overrides,
});

const createMock = vi.mocked(shuffleApi.createSession);
const getMock = vi.mocked(shuffleApi.getSession);
const deleteMock = vi.mocked(shuffleApi.deleteSession);

const startSequentialSource = (ids: string[]) => {
  const store = usePlayerStore();
  const files = ids.map((id) => makeFile(id));
  store.setSource(files, { isVideo: false, playlistId: null }, 1, 0, 1, async () => ({
    items: files,
    totalPages: 1,
  }));
  return store;
};

describe("stream-player-shuffle", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("enables shuffle with an anchor and preserves the current stream", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));

    await store.toggleShuffle();

    expect(createMock).toHaveBeenCalledTimes(1);
    expect(createMock.mock.calls[0][0]).toMatchObject({ anchorFileId: "a" });
    expect(store.shuffled).toBe(true);
    expect(store.shuffleSessionId).toBe("sess-1");
    expect(store.shufflePosition).toBe(0);
    expect(store.activeFile?.fileId).toBe("a");
    expect(store.shuffleBusy).toBe(false);
  });

  it("retries unanchored after an anchor conflict and keeps manual playback", async () => {
    const store = startSequentialSource(["a", "b"]);
    store.playNow([makeFile("search-hit")]);
    createMock.mockRejectedValueOnce({ response: { status: 409 } });
    createMock.mockResolvedValueOnce(makeSession("sess-2", ["a", "b"]));

    await store.toggleShuffle();

    expect(createMock).toHaveBeenCalledTimes(2);
    expect(createMock.mock.calls[1][0].anchorFileId ?? null).toBeNull();
    expect(store.shuffled).toBe(true);
    expect(store.shufflePosition).toBe(-1);
    expect(store.activeFile?.fileId).toBe("search-hit");
    expect(store.activePlaybackOrigin).toBe("manual");
  });

  it("keeps sequential state when enabling fails", async () => {
    const store = startSequentialSource(["a", "b"]);
    createMock.mockRejectedValueOnce({ response: { status: 404 } });

    await store.toggleShuffle();

    expect(store.shuffled).toBe(false);
    expect(store.shuffleSessionId).toBeNull();
    expect(store.shuffleError).toMatch(/no longer available/);
    expect(store.activeFile?.fileId).toBe("a");
    expect(store.sourceList).toHaveLength(2);
  });

  it("disables shuffle through the canonical anchor page without touching playback", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));
    await store.toggleShuffle();
    await store.next();
    const playingFile = store.activeFile;
    expect(store.shufflePosition).toBe(1);

    vi.mocked(streamingApi.getFilesForStreaming).mockResolvedValueOnce({
      items: [makeFile("a"), makeFile("b"), makeFile("c")],
      currentPage: 1,
      pageSize: 50,
      totalCount: 3,
      totalPages: 1,
      hasPrevious: false,
      hasNext: false,
    });
    deleteMock.mockResolvedValueOnce(undefined);

    await store.toggleShuffle();

    expect(store.shuffled).toBe(false);
    expect(store.activeFile?.fileId).toBe("b");
    expect(store.activeFile).toBe(playingFile);
    expect(store.currentIndex).toBe(1);
    expect(deleteMock).toHaveBeenCalledWith("sess-1");
  });

  it("advances through gaps and follows continuation into later ranges", async () => {
    const store = startSequentialSource(["a", "b", "c", "d", "e"]);
    createMock.mockResolvedValueOnce({
      ...makeSession("sess-1", ["a", "b", "d", "e"]),
      totalCount: 5,
      scannedCount: 5,
      nextOffset: null,
      items: [
        { position: 0, file: makeFile("a") },
        { position: 1, file: makeFile("b") },
        { position: 3, file: makeFile("d") },
        { position: 4, file: makeFile("e") },
      ],
    });
    await store.toggleShuffle();

    await store.next();
    expect(store.activeFile?.fileId).toBe("b");
    await store.next();
    expect(store.activeFile?.fileId).toBe("d");
    expect(store.shufflePosition).toBe(3);
    await store.previous();
    expect(store.activeFile?.fileId).toBe("b");
    expect(getMock).not.toHaveBeenCalled();
  });

  it("fetches later ranges on demand and computes nextOffset from scanned slots", async () => {
    const store = startSequentialSource(["a", "b", "c", "d"]);
    createMock.mockResolvedValueOnce({
      ...makeSession("sess-1", ["a", "b"]),
      totalCount: 4,
      scannedCount: 2,
      nextOffset: 2,
    });
    await store.toggleShuffle();

    getMock.mockResolvedValueOnce({
      ...makeSession("sess-1", ["c", "d"], { totalCount: 4 }),
      offset: 2,
      scannedCount: 2,
      nextOffset: null,
      items: [
        { position: 2, file: makeFile("c") },
        { position: 3, file: makeFile("d") },
      ],
    });

    await store.next();
    expect(store.activeFile?.fileId).toBe("b");
    await store.next();
    expect(getMock).toHaveBeenCalledWith("sess-1", 0, 50);
    expect(store.activeFile?.fileId).toBe("c");
    expect(store.shufflePosition).toBe(2);
  });

  it("keeps manual queue entries out of the source cursor", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    store.enqueue(makeFile("b"));
    await store.next();

    expect(store.activeFile?.fileId).toBe("b");
    expect(store.activePlaybackOrigin).toBe("manual");
    expect(store.shufflePosition).toBe(0);
    await store.next();
    expect(store.shufflePosition).toBe(1);
  });

  it("serializes fast advances and drops stale generations", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const first = store.next();
    const second = store.next();
    await Promise.all([first, second]);
    expect(store.shufflePosition).toBe(2);

    const pending = store.next();
    store.setSource([makeFile("x")], { isVideo: false, playlistId: null }, 1, 0, 1, async () => ({
      items: [makeFile("x")],
      totalPages: 1,
    }));
    await pending;
    expect(store.activeFile?.fileId).toBe("x");
    expect(store.shuffled).toBe(false);
  });

  it("creates a fresh avoid-first cycle on repeat-all at the snapshot end", async () => {
    const store = startSequentialSource(["a", "b"]);
    store.toggleLoop();
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const firstOrder = store.upNextItems.map((i) => i.file.fileId);
    createMock.mockResolvedValueOnce(makeSession("sess-2", ["b", "a"]));
    deleteMock.mockResolvedValueOnce(undefined);

    await store.next();
    await store.next();
    await store.next();

    expect(createMock.mock.calls[1][0]).toMatchObject({
      avoidFirstFileId: firstOrder.at(-1) ?? "b",
    });
    expect(store.shuffleSessionId).toBe("sess-2");
    expect(deleteMock).toHaveBeenCalledWith("sess-1");
    expect(store.queueEnded).toBe(false);
  });

  it("replays with a fresh cycle and ends empty snapshots", async () => {
    const store = startSequentialSource(["a"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a"], { anchorPosition: 0 }));
    await store.toggleShuffle();
    await store.next();
    expect(store.queueEnded).toBe(true);

    createMock.mockResolvedValueOnce(makeSession("sess-2", ["a"]));
    await store.restartQueue();
    expect(store.shuffleSessionId).toBe("sess-2");
    expect(store.queueEnded).toBe(false);

    createMock.mockResolvedValueOnce(makeSession("sess-3", []));
    store.shufflePosition = 0;
    await store.restartQueue();
    expect(store.queueEnded).toBe(true);
  });

  it("caps buffered shuffle metadata at ten ranges", async () => {
    const store = startSequentialSource(["a"]);
    const total = 600;
    const rangeAt = (offset: number) => {
      const end = Math.min(offset + 50, total);
      const ids = Array.from({ length: end - offset }, (_, i) => `t-${offset + i}`);
      return makeSession("sess-1", ids, {
        totalCount: total,
        offset,
        scannedCount: ids.length,
        nextOffset: end < total ? end : null,
        items: ids.map((fileId, i) => ({ position: offset + i, file: makeFile(fileId) })),
      });
    };
    createMock.mockResolvedValueOnce(rangeAt(0));
    getMock.mockImplementation(async (_id: string, offset: number) => rangeAt(offset));
    await store.toggleShuffle();

    for (const position of [550, 500, 450, 400, 350, 300, 250, 200, 150, 100, 50]) {
      await store.playFromSource(position);
    }

    expect(store.shuffleEntries.size).toBeLessThanOrEqual(500);
    expect(store.activeFile?.fileId).toBe("t-50");
  });

  it("refetches evicted ranges while walking back through a large order", async () => {
    const store = startSequentialSource(["a"]);
    const total = 600;
    const rangeAt = (offset: number) =>
      makeSession("sess-1", [], {
        totalCount: total,
        offset,
        scannedCount: Math.min(50, total - offset),
        nextOffset: offset + 50 < total ? offset + 50 : null,
        items: Array.from({ length: Math.min(50, total - offset) }, (_, i) => ({
          position: offset + i,
          file: makeFile(`t-${offset + i}`),
        })),
      });
    createMock.mockResolvedValueOnce(rangeAt(0));
    getMock.mockImplementation(async (_id: string, offset: number) => rangeAt(offset));
    await store.toggleShuffle();
    for (const position of [100, 150, 200, 250, 300, 350, 400, 450, 500, 550]) {
      await store.playFromSource(position);
    }
    for (let i = 0; i < 551; i++) await store.previous();

    expect(store.shufflePosition).toBe(0);
    expect(store.activeFile?.fileId).toBe("t-0");
    expect(getMock.mock.calls.filter((call) => call[1] === 0).length).toBeGreaterThan(0);
  });

  it("ignores a shuffle creation that resolves after the source changes", async () => {
    const store = startSequentialSource(["a"]);
    let resolveCreate!: (value: ShuffleSessionResponse) => void;
    createMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveCreate = resolve;
        }),
    );
    const enabling = store.toggleShuffle();
    await vi.waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    store.setSource(
      [makeFile("video")],
      { isVideo: true, playlistId: null },
      1,
      0,
      1,
      async () => ({ items: [makeFile("video")], totalPages: 1 }),
    );
    resolveCreate(makeSession("late", ["a"], { anchorPosition: 0 }));
    await enabling;

    expect(store.activeFile?.fileId).toBe("video");
    expect(store.shuffleSessionId).toBeNull();
    expect(store.shuffled).toBe(false);
  });

  it("lets Previous navigate the source while future manual entries are queued", async () => {
    const store = startSequentialSource(["a", "b"]);
    await store.playFromSource(1);
    store.enqueue(makeFile("queued"));
    await store.previous();
    expect(store.activeFile?.fileId).toBe("a");
    expect(store.userQueue.map((file) => file.fileId)).toEqual(["queued"]);
  });

  it("jumps queue rows by absolute position", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["c", "a", "b"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const rows = store.upNextItems.filter((i) => i.kind === "source");
    expect(rows.map((i) => i.sourceIndex)).toEqual([1, 2]);
    await store.playFromSource(2);
    expect(store.activeFile?.fileId).toBe("b");
    expect(store.shufflePosition).toBe(2);
  });

  it("coalesces duplicate end events for the same item", async () => {
    const store = startSequentialSource(["a", "b"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    store.handleTrackEnded();
    store.handleTrackEnded();
    await Promise.resolve();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(store.shufflePosition).toBe(1);
  });

  it("parks the shuffle session on source switch without clearing the queue", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    store.enqueue(makeFile("manual"));
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const files = [makeFile("x"), makeFile("y")];
    store.setSource(files, { isVideo: false, playlistId: "pl-1" }, 1, 0, 1, async () => ({
      items: files,
      totalPages: 1,
    }));

    expect(store.shuffled).toBe(false);
    expect(deleteMock).not.toHaveBeenCalled();
    expect(store.parkedContext?.sessionId).toBe("sess-1");
    expect(store.parkedContext?.position).toBe(0);
    expect(store.userQueue.map((file) => file.fileId)).toEqual(["manual"]);
    expect(store.activeFile?.fileId).toBe("x");
  });

  it("resumes the parked shuffle when the playlist ends", async () => {
    const store = startSequentialSource(["a", "b", "c"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b", "c"], { anchorPosition: 0 }));
    await store.toggleShuffle();
    await store.next();

    const files = [makeFile("x")];
    store.setSource(files, { isVideo: false, playlistId: "pl-1" }, 1, 0, 1, async () => ({
      items: files,
      totalPages: 1,
    }));

    getMock.mockResolvedValueOnce(
      makeSession("sess-1", ["a", "b", "c"], {
        totalCount: 3,
        offset: 0,
        scannedCount: 3,
        nextOffset: null,
      }),
    );
    await store.next();

    expect(store.shuffleSessionId).toBe("sess-1");
    expect(store.shuffled).toBe(true);
    expect(store.activeFile?.fileId).toBe("b");
    expect(store.shuffleNotice).toMatch(/previous shuffle/);
    expect(store.parkedContext).toBeNull();
    expect(store.queueEnded).toBe(false);
  });

  it("mints a fresh cycle when the parked session is gone", async () => {
    const store = startSequentialSource(["a"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const files = [makeFile("x")];
    store.setSource(files, { isVideo: false, playlistId: "pl-1" }, 1, 0, 1, async () => ({
      items: files,
      totalPages: 1,
    }));

    getMock.mockRejectedValueOnce({ response: { status: 404 } });
    createMock.mockResolvedValueOnce(makeSession("sess-2", ["q"]));
    await store.next();

    expect(store.shuffleSessionId).toBe("sess-2");
    expect(store.activeFile?.fileId).toBe("q");
    expect(store.shuffleNotice).toMatch(/fresh cycle/);
  });

  it("replaces the park on a second switch and releases the old handle", async () => {
    const store = startSequentialSource(["a"]);
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const first = [makeFile("x")];
    store.setSource(first, { isVideo: false, playlistId: "pl-1" }, 1, 0, 1, async () => ({
      items: first,
      totalPages: 1,
    }));
    expect(store.parkedContext?.sessionId).toBe("sess-1");

    const second = [makeFile("y")];
    store.setSource(second, { isVideo: false, playlistId: "pl-2" }, 1, 0, 1, async () => ({
      items: second,
      totalPages: 1,
    }));
    expect(store.parkedContext).toBeNull();
    expect(deleteMock).toHaveBeenCalledWith("sess-1");
  });

  it("repeat-all wraps the playlist in place and keeps the park", async () => {
    const store = startSequentialSource(["a", "b"]);
    store.toggleLoop();
    createMock.mockResolvedValueOnce(makeSession("sess-1", ["a", "b"], { anchorPosition: 0 }));
    await store.toggleShuffle();

    const files = [makeFile("x")];
    store.setSource(files, { isVideo: false, playlistId: "pl-1" }, 1, 0, 1, async () => ({
      items: files,
      totalPages: 1,
    }));
    await store.next();

    expect(store.activeFile?.fileId).toBe("x");
    expect(store.shuffleSessionId).toBeNull();
    expect(createMock).toHaveBeenCalledTimes(1);
    expect(store.parkedContext?.sessionId).toBe("sess-1");
    expect(store.queueEnded).toBe(false);
  });

  it("appends a playlist to the queue and cold-starts when idle", async () => {
    const store = usePlayerStore();
    vi.mocked(streamingApi.getFilesForStreaming)
      .mockResolvedValueOnce({
        items: [makeFile("p1"), makeFile("p2")],
        currentPage: 1,
        pageSize: 500,
        totalCount: 2,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      })
      .mockResolvedValueOnce({
        items: [],
        currentPage: 2,
        pageSize: 500,
        totalCount: 2,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      });

    const total = await store.appendPlaylist("pl-9");

    expect(total).toBe(2);
    expect(store.userQueue).toHaveLength(1);
    expect(store.activeFile?.fileId).toBe("p1");
    expect(store.activePlaybackOrigin).toBe("manual");
    expect(vi.mocked(streamingApi.getFilesForStreaming).mock.calls[0][0]).toMatchObject({
      playlistId: "pl-9",
      pageSize: 500,
    });
  });

  it("loads further shuffle ranges on demand", async () => {
    const store = startSequentialSource(["a"]);
    const total = 200;
    const rangeAt = (offset: number) => {
      const end = Math.min(offset + 50, total);
      const ids = Array.from({ length: end - offset }, (_, i) => `t-${offset + i}`);
      return makeSession("sess-1", ids, {
        totalCount: total,
        offset,
        scannedCount: ids.length,
        nextOffset: end < total ? end : null,
        items: ids.map((fileId, i) => ({ position: offset + i, file: makeFile(fileId) })),
      });
    };
    createMock.mockResolvedValueOnce(rangeAt(0));
    getMock.mockImplementation(async (_id: string, offset: number) => rangeAt(offset));
    await store.toggleShuffle();

    await store.loadMoreShuffle();

    expect(store.shuffleEntries.size).toBe(100);
    expect(store.shuffleLoadingMore).toBe(false);
  });
});
