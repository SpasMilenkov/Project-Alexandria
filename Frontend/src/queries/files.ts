import { defineQueryOptions } from "@pinia/colada";

import type { FileSearchQuery } from "@/schemas/search";
import type { PaginationParams } from "@/types/pagination-params";

import { fileApi } from "@/api/file";

export const FILES_QUERY_KEYS = {
  getFile: (id: string) => [...FILES_QUERY_KEYS.root, "file", id],
  root: ["files"] as const,
  rootFiles: (params: PaginationParams) => [
    ...FILES_QUERY_KEYS.root,
    "root-sub-files",
    params.page,
    params.pageSize,
    params.sortDirection,
    params.SortBy,
  ],
  searchFiles: (query: FileSearchQuery) => [
    ...FILES_QUERY_KEYS.root,
    "search-files",
    query.nameContains ?? null,
    query.isDeleted,
    query.isShared,
    query.isStarred,
    query.currentPage,
    query.sortDirection,
    query.sortBy,
    query.pageSize,
  ],
  // unversioned, resolves to "current version" server-side, so its result
  // can go stale the moment a new version becomes active
  preview: (fileId: string) => [...FILES_QUERY_KEYS.root, "preview", fileId],
  // versionId is part of the key, and a version's content never changes,
  // so this key is safe to cache indefinitely once populated
  previewByVersion: (fileId: string, versionId: string) => [
    ...FILES_QUERY_KEYS.root,
    "preview-by-version",
    fileId,
    versionId,
  ],
  subFiles: ({ id, params }: { id: string; params: PaginationParams }) =>
    [
      ...FILES_QUERY_KEYS.root,
      "sub-files",
      id,
      params.page,
      params.pageSize,
      params.sortDirection,
      params.SortBy,
    ] as const,
  versionSignedUrl: (id: string) => [...FILES_QUERY_KEYS.root, "version-signed-url", id],
  versionsForFile: ({ id, page, pageSize }: { id: string; page: number; pageSize: number }) => [
    ...FILES_QUERY_KEYS.root,
    "versions-for-file",
    id,
    page,
    pageSize,
  ],
};

export const subFiles = defineQueryOptions(
  ({ id, params }: { id: string; params: PaginationParams }) => ({
    key: FILES_QUERY_KEYS.subFiles({ id, params }),
    placeholderData: (prev) => prev,
    query: () => fileApi.getSubFiles(id, params),
  }),
);

// Unversioned convenience query, "whatever's current." Keep the TTL short,
// this can legitimately go stale the moment someone uploads a new version.
export const getPreview = defineQueryOptions((id: string) => ({
  key: FILES_QUERY_KEYS.preview(id),
  query: () => fileApi.getPreview(id),
  refetchOnMount: false,
  staleTime: 30_000,
}));

// Versioned query. A given (fileId, versionId) pair's preview content is
// permanent — the version can't change out from under this key — so it's
// safe to treat as effectively immutable client-side, same reasoning as the
// immutable Cache-Control on the byte-serving endpoint itself.
export const getPreviewByVersion = defineQueryOptions(
  ({ fileId, versionId }: { fileId: string; versionId: string }) => ({
    key: FILES_QUERY_KEYS.previewByVersion(fileId, versionId),
    query: () => fileApi.getPreviewByVersion(fileId, versionId),
    staleTime: Infinity,
    gcTime: 24 * 60 * 60 * 1000, // 24h, keeps scroll-back from refetching within a session
  }),
);

export const rootFiles = defineQueryOptions((params: PaginationParams) => ({
  key: FILES_QUERY_KEYS.rootFiles(params),
  placeholderData: (prev) => prev,
  query: () => fileApi.getRootFiles(params),
}));

export const searchFile = defineQueryOptions((query: FileSearchQuery) => ({
  key: FILES_QUERY_KEYS.searchFiles(query),
  query: () => fileApi.searchFiles(query),
}));

export const getVersionsForFile = defineQueryOptions(
  (query: { id: string; page: number; pageSize: number }) => ({
    key: FILES_QUERY_KEYS.versionsForFile(query),
    query: () => fileApi.getVersionsForFile(query),
    staleTime: 30_000,
  }),
);

export const getVersionDownloadUrl = defineQueryOptions((id: string) => ({
  key: FILES_QUERY_KEYS.versionSignedUrl(id),
  query: () => fileApi.downloadFileVersion(id),
}));

export const getFile = defineQueryOptions((id: string) => ({
  key: FILES_QUERY_KEYS.getFile(id),
  query: () => fileApi.getFile(id),
}));
