import { defineQueryOptions } from "@pinia/colada";

import type { PaginationParams } from "@/types/pagination-params";

import { type SearchDirectoryRequest, directoryApi } from "@/api/directory";

const normalizeSearchKey = (req: SearchDirectoryRequest) => ({
  deletedAfter: req.deletedAfter ?? null,
  deletedBefore: req.deletedBefore ?? null,
  directoryId: req.directoryId ?? null,
  hasFiles: req.hasFiles ?? null,
  hasSubdirectories: req.hasSubdirectories ?? null,
  isDeleted: req.isDeleted ?? null,
  isShared: req.isShared ?? null,
  isStarred: req.isStarred ?? null,
  nameContains: req.nameContains ?? null,
  ownerId: req.ownerId ?? null,
  page: req.page ?? 1,
  pageSize: req.pageSize ?? 20,
  parentDirectoryId: req.parentDirectoryId ?? null,
  sortBy: req.sortBy ?? "createdAt",
  sortDirection: req.sortDirection ?? "desc",
});

export const DIRECTORY_QUERY_KEYS = {
  byId: (id: string) => [...DIRECTORY_QUERY_KEYS.root, "by-id", id] as const,
  directoryPath: (id: string) => [...DIRECTORY_QUERY_KEYS.root, "path", id],
  root: ["directories"] as const,
  rootDirectories: (params: PaginationParams) => [
    ...DIRECTORY_QUERY_KEYS.root,
    "root-sub-directories",
    params.page ?? 1,
    params.pageSize ?? 20,
    params.SortBy ?? "name",
    params.sortDirection ?? "asc",
  ],
  searchDirectory: (req: SearchDirectoryRequest) => [
    ...DIRECTORY_QUERY_KEYS.root,
    "search",
    normalizeSearchKey(req),
  ],
  subDirectories: ({ id, params }: { id: string; params: PaginationParams }) =>
    [
      ...DIRECTORY_QUERY_KEYS.root,
      "sub-directories",
      id ?? null,
      params.page ?? 1,
      params.pageSize ?? 20,
      params.SortBy ?? "name",
      params.sortDirection ?? "asc",
    ] as const,
};

export const directoryById = defineQueryOptions(({ id }: { id: string }) => ({
  key: DIRECTORY_QUERY_KEYS.byId(id),
  query: () => directoryApi.getDirectory(id),
}));

export const subDirectories = defineQueryOptions(
  ({ id, params }: { id: string; params: PaginationParams }) => ({
    key: DIRECTORY_QUERY_KEYS.subDirectories({ id, params }),
    placeholderData: (prev) => prev,
    query: () => directoryApi.getSubDirectories(id, params),
  }),
);

export const rootDirectories = defineQueryOptions((params: PaginationParams) => ({
  key: DIRECTORY_QUERY_KEYS.rootDirectories(params),
  placeholderData: (prev) => prev,
  query: () => directoryApi.getRooSubDirectories(params),
}));

export const directoryPath = defineQueryOptions((id: string) => ({
  enabled: Boolean(id),
  key: DIRECTORY_QUERY_KEYS.directoryPath(id),
  placeholderData: (prev) => prev,
  query: () => directoryApi.getDirectoryPath(id),
}));

export const searchDirectory = defineQueryOptions((req: SearchDirectoryRequest) => ({
  key: DIRECTORY_QUERY_KEYS.searchDirectory(req),
  query: () => directoryApi.searchDirectory(req),
}));
