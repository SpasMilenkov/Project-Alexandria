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

export const AUDIO_LIBRARY_REF: SourceDescriptor = { isVideo: false, playlistId: null };
export const VIDEO_LIBRARY_REF: SourceDescriptor = { isVideo: true, playlistId: null };

export const AUDIO_LIBRARY_LABEL = "Music library";
export const VIDEO_LIBRARY_LABEL = "Video library";

export const libraryLabel = (ref: SourceDescriptor): string =>
  ref.isVideo ? VIDEO_LIBRARY_LABEL : AUDIO_LIBRARY_LABEL;

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
  totalCount: number;
  currentPage: number;
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
  return {
    items: result.items,
    totalPages: result.totalPages,
    totalCount: result.totalCount,
    currentPage: result.currentPage,
  };
};

export const fetchAnchorPage = async (
  descriptor: SourceDescriptor,
  anchor: SourceAnchor,
  pageSize: number = LIBRARY_PAGE_SIZE,
): Promise<PageResult> => {
  const result = await streamingApi.getFilesForStreaming({
    page: 1,
    pageSize,
    isVideo: descriptor.isVideo,
    playlistId: descriptor.playlistId,
    query: null,
    anchorFileId: anchor.fileId,
    anchorPlaylistItemId: anchor.playlistItemId ?? null,
  } satisfies GetFilesForStreamingQuery);
  return {
    items: result.items,
    totalPages: result.totalPages,
    totalCount: result.totalCount,
    currentPage: result.currentPage,
  };
};

export const entryIdentity = (file: MediaFileDto): string => file.playlistItemId ?? file.fileId;

export const anchorMatches = (file: MediaFileDto, anchor: SourceAnchor): boolean =>
  anchor.playlistItemId
    ? file.playlistItemId === anchor.playlistItemId
    : file.fileId === anchor.fileId;

export const indexOfAnchor = (items: MediaFileDto[], anchor: SourceAnchor): number =>
  items.findIndex((file) => anchorMatches(file, anchor));

export const positionOf = (currentPage: number, indexInPage: number): number =>
  (currentPage - 1) * LIBRARY_PAGE_SIZE + indexInPage;

export const anchorForFile = (file: MediaFileDto): SourceAnchor =>
  file.playlistItemId
    ? { fileId: file.fileId, playlistItemId: file.playlistItemId }
    : { fileId: file.fileId };

export const isAudioFile = (file: MediaFileDto | null): boolean =>
  (file?.mimeType ?? "").startsWith("audio/");

export const toAnchor = (ref: SourceDescriptor, file: MediaFileDto): SourceAnchor => {
  if (ref.playlistId && file.playlistItemId) {
    return { fileId: file.fileId, playlistItemId: file.playlistItemId };
  }
  return { fileId: file.fileId };
};

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

/**
 * Collects a playlist's playable tracks in playlist order so the queue can be
 * assigned once instead of pushed item by item.
 */
export const fetchPlaylistTracks = async (playlistId: string): Promise<MediaFileDto[]> => {
  const collected: MediaFileDto[] = [];
  let page = 1;
  let totalPages = 1;
  do {
    const result = await streamingApi.getFilesForStreaming({
      page,
      pageSize: APPEND_PAGE_SIZE,
      playlistId,
      isVideo: false,
      query: null,
    });
    totalPages = result.totalPages;
    collected.push(...result.items);
    page++;
  } while (page <= totalPages);
  return collected;
};
