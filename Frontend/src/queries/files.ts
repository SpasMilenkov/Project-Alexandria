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
  signedUrl: (id: string) => [...FILES_QUERY_KEYS.root, "signed-url", id],
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

export const getPreview = defineQueryOptions((id: string) => ({
  key: FILES_QUERY_KEYS.signedUrl(id),
  query: () => fileApi.getPreview(id),
  refetchOnMount: true,
  staleTime: 30000,
}));

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
