import { type GetFilesForStreamingQuery, type MediaFileDto, streamingApi } from "@/api/streaming";

export const LIBRARY_PAGE_SIZE = 50;

export interface SourceDescriptor {
  isVideo: boolean;
  playlistId: string | null;
}

export interface SourceAnchor {
  fileId: string;
  playlistItemId?: string | null;
}

export const sameSource = (a: SourceDescriptor | null, b: SourceDescriptor | null): boolean => {
  if (!a || !b) return a === b;
  return a.isVideo === b.isVideo && (a.playlistId ?? null) === (b.playlistId ?? null);
};

export const describeSource = (descriptor: SourceDescriptor): string =>
  descriptor.playlistId
    ? `playlist:${descriptor.playlistId}`
    : `library:${descriptor.isVideo ? "video" : "audio"}`;

export const descriptorForFile = (file: MediaFileDto): SourceDescriptor => ({
  isVideo: !file.mimeType.startsWith("audio/"),
  playlistId: null,
});

export interface PageResult {
  items: MediaFileDto[];
  totalPages: number;
}

export const fetchSequentialPage = async (
  descriptor: SourceDescriptor,
  page: number,
  pageSize: number = LIBRARY_PAGE_SIZE,
): Promise<PageResult> => {
  const result = await streamingApi.getFilesForStreaming({
    page,
    pageSize,
    isVideo: descriptor.isVideo,
    playlistId: descriptor.playlistId,
    query: null,
  } satisfies GetFilesForStreamingQuery);
  return { items: result.items, totalPages: result.totalPages };
};

export const fetchAnchorPage = async (
  descriptor: SourceDescriptor,
  anchor: SourceAnchor,
  pageSize: number = LIBRARY_PAGE_SIZE,
): Promise<PageResult & { currentPage: number }> => {
  const result = await streamingApi.getFilesForStreaming({
    page: 1,
    pageSize,
    isVideo: descriptor.isVideo,
    playlistId: descriptor.playlistId,
    query: null,
    anchorFileId: anchor.fileId,
    anchorPlaylistItemId: anchor.playlistItemId ?? null,
  } satisfies GetFilesForStreamingQuery);
  return { items: result.items, totalPages: result.totalPages, currentPage: result.currentPage };
};

export const entryIdentity = (file: MediaFileDto): string => file.playlistItemId ?? file.fileId;

export const shuffleRowKey = (sessionId: string, position: number): string =>
  `${sessionId}:${position}`;

export const loadPlaylistFirstPage = (playlistId: string, pageSize: number = LIBRARY_PAGE_SIZE) =>
  streamingApi.getFilesForStreaming({
    page: 1,
    pageSize,
    playlistId,
    isVideo: false,
    query: null,
  });

export const loadPlaylistAnchorPage = (
  playlistId: string,
  fileId: string,
  itemId: string,
  pageSize: number = LIBRARY_PAGE_SIZE,
) =>
  streamingApi.getFilesForStreaming({
    page: 1,
    pageSize,
    playlistId,
    isVideo: false,
    query: null,
    anchorFileId: fileId,
    anchorPlaylistItemId: itemId,
  });

export const findPlaylistItemIndex = (items: MediaFileDto[], itemId: string): number =>
  items.findIndex((f) => f.playlistItemId === itemId);

export const playlistPageFetcher =
  (playlistId: string, pageSize: number = LIBRARY_PAGE_SIZE) =>
  (page: number) =>
    streamingApi.getFilesForStreaming({
      page,
      pageSize,
      playlistId,
      isVideo: false,
      query: null,
    });

export const APPEND_PAGE_SIZE = 500;

export const appendPlaylistToQueue = async (
  playlistId: string,
  enqueue: (file: MediaFileDto) => void,
): Promise<number> => {
  let page = 1;
  let totalPages = 1;
  let totalCount = 0;
  do {
    const result = await streamingApi.getFilesForStreaming({
      page,
      pageSize: APPEND_PAGE_SIZE,
      playlistId,
      isVideo: false,
      query: null,
    });
    totalPages = result.totalPages;
    totalCount = result.totalCount;
    for (const item of result.items) enqueue(item);
    page++;
  } while (page <= totalPages);
  return totalCount;
};
