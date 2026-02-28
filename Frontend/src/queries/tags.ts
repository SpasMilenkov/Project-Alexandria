import { defineQueryOptions } from "@pinia/colada";

import type { SearchFilesByTagsSchema, SearchTagsSchema } from "@/schemas/tag";
import type { PaginationParams } from "@/types/pagination-params";

import { tagApi } from "@/api/tag";

const normalizeFilters = (filters: SearchTagsSchema) => {
  const { page, pageSize, ...rest } = filters;

  return {
    filters: Object.fromEntries(
      Object.entries(rest).filter(([_, v]) => v !== undefined && v !== null),
    ),
    page,
    pageSize,
  };
};

export const TAGS_QUERY_KEYS = {
  getAllTags: (params: PaginationParams) => [
    ...TAGS_QUERY_KEYS.root,
    params.page,
    params.pageSize,
    params.SortBy,
    params.sortDirection,
  ],
  getTagsForFile: (fileId: string) => [...TAGS_QUERY_KEYS.root, "tags-for-file", fileId],
  root: ["tags"] as const,
  searchFileByTags: (filters: SearchFilesByTagsSchema) => [
    ...TAGS_QUERY_KEYS.root,
    "file-by-tags",
    normalizeFilters(filters),
  ],
  searchTag: (filters: SearchTagsSchema) => [
    ...TAGS_QUERY_KEYS.root,
    "search",
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
  staleTime: 30000,
}));

export const getTagsForFile = defineQueryOptions((fileId: string) => ({
  enabled: Boolean(fileId),
  key: TAGS_QUERY_KEYS.getTagsForFile(fileId),
  query: () => tagApi.getTagsForFile(fileId),
  staleTime: 60000,
}));

export const searchFileByTags = defineQueryOptions((filters: SearchFilesByTagsSchema) => ({
  key: TAGS_QUERY_KEYS.searchFileByTags(filters),
  query: () => tagApi.searchFilesByTags(filters),
}));
