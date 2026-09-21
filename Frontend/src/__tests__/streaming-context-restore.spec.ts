import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

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

const makeFile = (fileId: string): MediaFileDto => ({
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
  playlistItemId: null,
  isVideo: false,
  segmentPrefix: null,
});

const getMock = vi.mocked(shuffleApi.getSession);
const createMock = vi.mocked(shuffleApi.createSession);
const filesMock = vi.mocked(streamingApi.getFilesForStreaming);

describe("streaming-context-restore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("clears owner-bound state when the owner changes", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.activeFile = makeFile("a");
    store.enqueue(makeFile("q"));
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.shuffleSessionId = "sess-1";
    store.shufflePosition = 3;

    await store.restorePlaybackContext("owner-2");

    expect(store.activeFile).toBeNull();
    expect(store.userQueue).toHaveLength(0);
    expect(store.sourceDescriptor).toBeNull();
    expect(store.shuffleSessionId).toBeNull();
    expect(store.playbackOwnerId).toBe("owner-2");
    expect(getMock).not.toHaveBeenCalled();
  });

  it("reconnects a persisted shuffle session and validates its source", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.sourceAnchor = { fileId: "b" };
    store.shuffleSessionId = "sess-1";
    store.shufflePosition = 1;
    store.shuffleTotalCount = 3;
    store.activeFile = makeFile("b");

    getMock.mockResolvedValueOnce({
      sessionId: "sess-1",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: "",
      expiresAt: "",
      totalCount: 3,
      anchorPosition: 0,
      offset: 1,
      scannedCount: 2,
      nextOffset: null,
      items: [
        { position: 1, file: makeFile("b") },
        { position: 2, file: makeFile("c") },
      ],
    });

    await store.restorePlaybackContext("owner-1");

    expect(getMock).toHaveBeenCalledWith("sess-1", 1, 50);
    expect(store.shuffled).toBe(true);
    expect(store.activeFile?.fileId).toBe("b");
    expect(store.shuffleRestoring).toBe(false);
  });

  it("drops the handle when the reconnected source differs", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.shuffleSessionId = "sess-1";
    store.shufflePosition = 0;
    store.shuffleTotalCount = 2;

    getMock.mockResolvedValueOnce({
      sessionId: "sess-1",
      source: { isVideo: true, playlistId: null },
      algorithmVersion: 1,
      createdAt: "",
      expiresAt: "",
      totalCount: 2,
      anchorPosition: null,
      offset: 0,
      scannedCount: 2,
      nextOffset: null,
      items: [],
    });

    await store.restorePlaybackContext("owner-1");

    expect(store.shuffled).toBe(false);
    expect(store.shuffleSessionId).toBeNull();
  });

  it("rebuilds once with a notice after API restart and keeps manual playback", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.sourceAnchor = { fileId: "a" };
    store.shuffleSessionId = "gone";
    store.shufflePosition = -1;
    store.shuffleTotalCount = 2;
    store.activeFile = makeFile("manual");
    store.activePlaybackOrigin = "manual";

    getMock.mockRejectedValueOnce({ response: { status: 404 } });
    createMock.mockResolvedValueOnce({
      sessionId: "sess-2",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: "",
      expiresAt: "",
      totalCount: 2,
      anchorPosition: 0,
      offset: 0,
      scannedCount: 2,
      nextOffset: null,
      items: [
        { position: 0, file: makeFile("a") },
        { position: 1, file: makeFile("b") },
      ],
    });

    await store.restorePlaybackContext("owner-1");

    expect(store.shuffleSessionId).toBe("sess-2");
    expect(store.shuffleNotice).toMatch(/refreshed/);
    expect(store.activeFile?.fileId).toBe("manual");
    store.dismissShuffleNotice();
    expect(store.shuffleNotice).toBeNull();
  });

  it("retains context and offers retry on generic failures", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.shuffleSessionId = "sess-1";
    store.shufflePosition = 0;
    store.shuffleTotalCount = 2;

    getMock.mockRejectedValueOnce(new Error("boom"));

    await store.restorePlaybackContext("owner-1");

    expect(store.shuffleSessionId).toBe("sess-1");
    expect(store.shuffled).toBe(false);
    expect(store.shuffleError).toMatch(/reconnect/);
  });

  it("re-establishes the sequential window from the saved anchor", async () => {
    const store = usePlayerStore();
    store.playbackOwnerId = "owner-1";
    store.sourceDescriptor = { isVideo: false, playlistId: null };
    store.sourceAnchor = { fileId: "b" };
    store.activeFile = makeFile("b");

    filesMock.mockResolvedValueOnce({
      items: [makeFile("a"), makeFile("b"), makeFile("c")],
      currentPage: 2,
      pageSize: 50,
      totalCount: 3,
      totalPages: 1,
      hasPrevious: false,
      hasNext: false,
    });

    await store.restorePlaybackContext("owner-1");

    expect(filesMock).toHaveBeenCalledWith(expect.objectContaining({ anchorFileId: "b" }));
    expect(store.sourceList).toHaveLength(3);
    expect(store.activeFile?.fileId).toBe("b");
    expect(store.shuffled).toBe(false);
  });

  it("does nothing without an authenticated owner", async () => {
    const store = usePlayerStore();
    store.shuffleSessionId = "sess-1";

    await store.restorePlaybackContext(null);

    expect(getMock).not.toHaveBeenCalled();
    expect(filesMock).not.toHaveBeenCalled();
  });
});
