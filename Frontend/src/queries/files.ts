import { fileApi } from "@/api/file";
import type { FileSearchQuery } from "@/schemas/search";
import type { PaginationParams } from "@/types/pagination-params";
import { defineQueryOptions } from "@pinia/colada";

export const FILES_QUERY_KEYS = {
  root: ["files"] as const,
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
  signedUrl: (id: string) => [...FILES_QUERY_KEYS.root, "signed-url", id],
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
};

export const subFiles = defineQueryOptions(
  ({ id, params }: { id: string; params: PaginationParams }) => ({
    key: FILES_QUERY_KEYS.subFiles({ id, params }),
    query: () => fileApi.getSubFiles(id, params),
    placeholderData: (prev) => prev,
  }),
);

export const getPreview = defineQueryOptions((id: string) => ({
  key: FILES_QUERY_KEYS.signedUrl(id),
  query: () => fileApi.getPreview(id),
  refetchOnMount: true,
  staleTime: 60000,
}));

export const rootFiles = defineQueryOptions((params: PaginationParams) => ({
  key: FILES_QUERY_KEYS.rootFiles(params),
  query: () => fileApi.getRootFiles(params),
  placeholderData: (prev) => prev,
}));

export const searchFile = defineQueryOptions((query: FileSearchQuery) => ({
  key: FILES_QUERY_KEYS.searchFiles(query),
  query: () => fileApi.searchFiles(query),
}));
