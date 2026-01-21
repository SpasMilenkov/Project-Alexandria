import { defineQueryOptions } from "@pinia/colada";
import { directoryApi, type SearchDirectoryRequest } from "@/api/directory";
import type { PaginationParams } from "@/types/pagination-params";

const normalizeSearchKey = (req: SearchDirectoryRequest) => ({
  directoryId: req.directoryId ?? null,
  parentDirectoryId: req.parentDirectoryId ?? null,
  nameContains: req.nameContains ?? null,
  ownerId: req.ownerId ?? null,
  isShared: req.isShared ?? null,
  isDeleted: req.isDeleted ?? null,
  isStarred: req.isStarred ?? null,
  hasFiles: req.hasFiles ?? null,
  hasSubdirectories: req.hasSubdirectories ?? null,
  page: req.page ?? 1,
  pageSize: req.pageSize ?? 20,
  sortBy: req.sortBy ?? "createdAt",
  sortDirection: req.sortDirection ?? "desc",
});

export const DIRECTORY_QUERY_KEYS = {
  root: ["directories"] as const,
  byId: (id: string) => [...DIRECTORY_QUERY_KEYS.root, "by-id", id] as const,
  subDirectories: ({ id, params }: { id: string; params: PaginationParams }) =>
    [
      ...DIRECTORY_QUERY_KEYS.root,
      "sub-directories",
      id ?? null,
      params.page ?? 1,
      params.pageSize ?? 20,
      params.orderBy ?? "name",
      params.sortDirection ?? "asc",
    ] as const,
  rootDirectories: (params: PaginationParams) => [
    ...DIRECTORY_QUERY_KEYS.root,
    "root-sub-directories",
    params.page ?? 1,
    params.pageSize ?? 20,
    params.orderBy ?? "name",
    params.sortDirection ?? "asc",
  ],
  directoryPath: (id: string) => [...DIRECTORY_QUERY_KEYS.root, "path", id],
  searchDirectory: (req: SearchDirectoryRequest) => [
    ...DIRECTORY_QUERY_KEYS.root,
    "search",
    normalizeSearchKey(req),
  ],
};
export const directoryById = defineQueryOptions(({ id }: { id: string }) => ({
  key: DIRECTORY_QUERY_KEYS.byId(id),
  query: () => directoryApi.getDirectory(id),
}));

export const subDirectories = defineQueryOptions(
  ({ id, params }: { id: string; params: PaginationParams }) => ({
    key: DIRECTORY_QUERY_KEYS.subDirectories({ id, params }),
    query: () => directoryApi.getSubDirectories(id, params),
    placeholderData: (prev) => prev,
  }),
);

export const rootDirectories = defineQueryOptions(
  (params: PaginationParams) => ({
    key: DIRECTORY_QUERY_KEYS.rootDirectories(params),
    query: () => directoryApi.getRooSubDirectories(params),
    placeholderData: (prev) => prev,
  }),
);

export const directoryPath = defineQueryOptions((id: string) => ({
  key: DIRECTORY_QUERY_KEYS.directoryPath(id),
  query: () => directoryApi.getDirectoryPath(id),
  placeholderData: (prev) => prev,
  enabled: !!id
}));

export const searchDirectory = defineQueryOptions(
  (req: SearchDirectoryRequest) => ({
    key: DIRECTORY_QUERY_KEYS.searchDirectory(req),
    query: () => directoryApi.searchDirectory(req),
  }),
);
