import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { MediaFileDto } from "@/api/streaming";

import { type ShuffleSessionResponse, shuffleApi } from "@/api/shuffle";
import { usePlayerStore } from "@/stores/stream-player";

vi.mock("@/api/shuffle", () => ({
  SHUFFLE_BATCH_LIMIT: 50,
  httpStatus: (error: unknown) =>
    (error as { response?: { status?: number } })?.response?.status ?? null,
  isAuthError: () => false,
  shuffleApi: {
    newRequestId: vi.fn(() => "request"),
    createSession: vi.fn(),
    getSession: vi.fn(),
    deleteSession: vi.fn(),
  },
}));

vi.mock("@/api/streaming", () => ({
  streamingApi: { getFilesForStreaming: vi.fn() },
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

const session = (sessionId: string, fileIds: string[]): ShuffleSessionResponse => ({
  sessionId,
  source: { isVideo: false, playlistId: null },
  algorithmVersion: 1,
  createdAt: "2026-09-21T00:00:00Z",
  expiresAt: "2026-09-22T00:00:00Z",
  totalCount: fileIds.length,
  anchorPosition: null,
  offset: 0,
  scannedCount: fileIds.length,
  nextOffset: null,
  items: fileIds.map((fileId, position) => ({ position, file: file(fileId) })),
});

const deferred = <T>() => {
  let resolve: (value: T) => void = () => undefined;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
};

const startPlaylist = (id = "last") => {
  const store = usePlayerStore();
  const items = [file(id)];
  store.setSource(items, { isVideo: false, playlistId: "playlist" }, 1, 0, 1, () => ({
    items,
    totalPages: 1,
  }));
  return store;
};

describe("audio continuation recovery and cancellation", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
    vi.mocked(shuffleApi.deleteSession).mockResolvedValue(undefined);
    vi.mocked(shuffleApi.newRequestId).mockReturnValue("request");
  });

  it("reports a failed sequential page and resumes that page on retry", async () => {
    const store = usePlayerStore();
    const fetchPage = vi
      .fn()
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce({ items: [file("next-page")], totalPages: 2 });
    store.setSource([file("last")], { isVideo: false, playlistId: "playlist" }, 1, 0, 2, fetchPage);

    await expect(store.next()).resolves.toBeUndefined();
    expect(store.queueStatus).toBe("error");
    expect(store.activeFile?.fileId).toBe("last");
    await store.retryShuffle();

    expect(store.activeFile?.fileId).toBe("next-page");
    expect(shuffleApi.createSession).not.toHaveBeenCalled();
  });

  it("keeps a manually selected song when an older library lookup completes", async () => {
    const store = startPlaylist();
    const pending = deferred<ShuffleSessionResponse>();
    vi.mocked(shuffleApi.createSession).mockReturnValueOnce(pending.promise);
    const advancing = store.next();
    await vi.waitFor(() => expect(shuffleApi.createSession).toHaveBeenCalledOnce());

    store.playNow([file("chosen")]);
    pending.resolve(session("stale", ["unwanted"]));
    await advancing;

    expect(store.activeFile?.fileId).toBe("chosen");
    expect(shuffleApi.deleteSession).toHaveBeenCalledWith("stale");
  });

  it("does not apply an empty-library result after a source switch during cleanup", async () => {
    const store = startPlaylist();
    const cleanup = deferred<void>();
    vi.mocked(shuffleApi.createSession).mockResolvedValueOnce(session("empty", []));
    vi.mocked(shuffleApi.deleteSession).mockReturnValueOnce(cleanup.promise);
    const advancing = store.next();
    await vi.waitFor(() => expect(shuffleApi.deleteSession).toHaveBeenCalledWith("empty"));

    startPlaylist("replacement");
    cleanup.resolve();
    await advancing;

    expect(store.activeFile?.fileId).toBe("replacement");
    expect(store.queueEnded).toBe(false);
    expect(store.shuffleError).toBeNull();
  });

  it("consumes a saved shuffle whose remaining entries are no longer available", async () => {
    const store = startPlaylist();
    store.parkedContext = {
      descriptor: { isVideo: false, playlistId: "previous" },
      sessionId: "saved",
      position: 0,
      totalCount: 2,
    };
    vi.mocked(shuffleApi.getSession).mockImplementation(async (_id, offset) => ({
      ...session("saved", []),
      source: { isVideo: false, playlistId: "previous" },
      offset,
      totalCount: 2,
      scannedCount: 2 - offset,
    }));
    vi.mocked(shuffleApi.createSession).mockResolvedValueOnce(session("library", ["available"]));

    await store.next();

    expect(store.activeFile?.fileId).toBe("available");
    expect(store.parkedContext).toBeNull();
  });
});
