import { tagApi } from "@/api/tag";
import type { SearchFilesByTagsSchema, SearchTagsSchema } from "@/schemas/tag";
import type { PaginationParams } from "@/types/pagination-params";
import { defineQueryOptions } from "@pinia/colada";

const normalizeFilters = (filters: SearchTagsSchema) => {
  const { page, pageSize, ...rest } = filters;

  return {
    page,
    pageSize,
    filters: Object.fromEntries(
      Object.entries(rest).filter(([_, v]) => v !== undefined && v !== null),
    ),
  };
};

export const TAGS_QUERY_KEYS = {
  root: ["tags"] as const,
  getAllTags: (params: PaginationParams) => [
    ...TAGS_QUERY_KEYS.root,
    params.page,
    params.pageSize,
    params.orderBy,
    params.sortDirection,
  ],
  searchTag: (filters: SearchTagsSchema) => [
    ...TAGS_QUERY_KEYS.root,
    "search",
    normalizeFilters(filters),
  ],
  getTagsForFile: (fileId: string) => [
    ...TAGS_QUERY_KEYS.root,
    "tags-for-file",
    fileId,
  ],
  searchFileByTags: (filters: SearchFilesByTagsSchema) => [
    ...TAGS_QUERY_KEYS.root,
    "file-by-tags",
    normalizeFilters(filters),
  ],
};

export const getAllTags = defineQueryOptions((params: PaginationParams) => ({
  key: TAGS_QUERY_KEYS.getAllTags(params),
  query: () => tagApi.getAllTags(params.page, params.pageSize),
}));

export const searchTag = defineQueryOptions((filters: SearchTagsSchema) => ({
  key: TAGS_QUERY_KEYS.searchTag(filters),
  query: () => tagApi.searchTags(filters),
}));

export const getTagsForFile = defineQueryOptions((fileId: string) => ({
  key: TAGS_QUERY_KEYS.getTagsForFile(fileId),
  query: () => tagApi.getTagsForFile(fileId),
  enabled: !!fileId,
  staleTime: 60000
}));

export const searchFileByTags = defineQueryOptions(
  (filters: SearchFilesByTagsSchema) => ({
    key: TAGS_QUERY_KEYS.searchFileByTags(filters),
    query: () => tagApi.searchFilesByTags(filters),
  }),
);
