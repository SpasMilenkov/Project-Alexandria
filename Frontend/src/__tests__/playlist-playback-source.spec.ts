import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { MediaFileDto } from "@/api/streaming";

import { streamingApi } from "@/api/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import {
  describeSource,
  findPlaylistItemIndex,
  loadPlaylistAnchorPage,
  loadPlaylistFirstPage,
  playlistPageFetcher,
  sameSource,
} from "@/utils/player-source";

vi.mock("@/api/shuffle", () => ({
  SHUFFLE_BATCH_LIMIT: 50,
  httpStatus: (err: unknown) =>
    (err as { response?: { status?: number } })?.response?.status ?? null,
  isAuthError: () => false,
  shuffleApi: {
    newRequestId: vi.fn(),
    createSession: vi.fn(),
    getSession: vi.fn(),
    deleteSession: vi.fn(),
  },
}));

vi.mock("@/api/streaming", () => ({
  streamingApi: { getFilesForStreaming: vi.fn() },
}));

const filesMock = vi.mocked(streamingApi.getFilesForStreaming);

const makeFile = (fileId: string, itemId?: string): MediaFileDto => ({
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
  transpilationJobId: `job-${fileId}`,
  playlistItemId: itemId ?? null,
  isVideo: false,
  segmentPrefix: null,
});

const pageOf = (items: MediaFileDto[], currentPage = 1) => ({
  items,
  currentPage,
  pageSize: 50,
  totalCount: items.length,
  totalPages: 1,
  hasPrevious: false,
  hasNext: false,
});

describe("playlist-playback-source", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("loads the first page without any capped queue population", async () => {
    filesMock.mockResolvedValueOnce(pageOf([makeFile("a")]));

    await loadPlaylistFirstPage("pl-1");

    expect(filesMock).toHaveBeenCalledWith({
      page: 1,
      pageSize: 50,
      playlistId: "pl-1",
      isVideo: false,
      query: null,
    });
  });

  it("passes the exact file and item anchor for selected-item starts", async () => {
    filesMock.mockResolvedValueOnce(pageOf([]));

    await loadPlaylistAnchorPage("pl-1", "file-a", "item-1");

    expect(filesMock).toHaveBeenCalledWith(
      expect.objectContaining({
        anchorFileId: "file-a",
        anchorPlaylistItemId: "item-1",
        playlistId: "pl-1",
      }),
    );
  });

  it("selects the exact occurrence among intentional duplicates", () => {
    const items = [makeFile("a", "item-1"), makeFile("b", "item-2"), makeFile("a", "item-3")];
    expect(findPlaylistItemIndex(items, "item-1")).toBe(0);
    expect(findPlaylistItemIndex(items, "item-3")).toBe(2);
    expect(findPlaylistItemIndex(items, "missing")).toBe(-1);
  });

  it("compares source descriptors by value", () => {
    expect(
      sameSource({ isVideo: false, playlistId: "p" }, { isVideo: false, playlistId: "p" }),
    ).toBe(true);
    expect(
      sameSource({ isVideo: false, playlistId: "p" }, { isVideo: false, playlistId: null }),
    ).toBe(false);
    expect(sameSource(null, null)).toBe(true);
    expect(sameSource(null, { isVideo: false, playlistId: null })).toBe(false);
    expect(describeSource({ isVideo: true, playlistId: null })).toBe("library:video");
    expect(describeSource({ isVideo: false, playlistId: "p" })).toBe("playlist:p");
  });

  it("starts playlist sources with identity instead of manual queues", async () => {
    const store = usePlayerStore();
    const items = [makeFile("a", "item-1"), makeFile("b", "item-2")];
    filesMock.mockResolvedValueOnce(pageOf(items));

    const result = await loadPlaylistFirstPage("pl-1");
    const fetcher = playlistPageFetcher("pl-1");
    store.setSource(result.items, { isVideo: false, playlistId: "pl-1" }, 1, 1, 1, fetcher);

    expect(store.sourceDescriptor).toEqual({ isVideo: false, playlistId: "pl-1" });
    expect(store.sourceAnchor).toEqual({ fileId: "b", playlistItemId: "item-2" });
    expect(store.activePlaylistId).toBe("pl-1");
    expect(store.userQueue).toHaveLength(0);
    expect(store.activeFile?.fileId).toBe("b");
  });

  it("keeps sequential duplicates distinct with item identity", () => {
    const store = usePlayerStore();
    const items = [makeFile("a", "item-1"), makeFile("a", "item-3")];
    store.setSource(
      items,
      { isVideo: false, playlistId: "pl-1" },
      1,
      1,
      1,
      async () => ({ items, totalPages: 1 }),
      { fileId: "a", playlistItemId: "item-3" },
    );

    expect(store.sourceList).toHaveLength(2);
    expect(store.sourceAnchor).toEqual({ fileId: "a", playlistItemId: "item-3" });
  });
});
