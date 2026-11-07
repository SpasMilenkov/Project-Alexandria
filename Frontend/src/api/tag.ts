import type {
  AddTagsToFileSchema,
  CreateTagSchema,
  SearchFilesByTagsSchema,
  SearchTagsSchema,
  UpdateTagSchema,
} from "@/schemas/tag";

import type { PaginatedResponse } from "./directory";

import { api } from "./client";

// Response Types
export interface TagDto {
  id: string;
  name: string;
  color: string;
  icon: string;
  description?: string | null;
  userId: string;
  createdAt: string;
  updatedAt: string | null;
  source?: number;
  confidence?: number | null;
  isSystem?: boolean;
  facet?: number | null;
  parentId?: string | null;
  parentName?: string | null;
}

export const TAG_FACET = { Genre: 0, Mood: 1 } as const;

export const FACET_LABELS: Record<number, string> = {
  [TAG_FACET.Genre]: "Genre",
  [TAG_FACET.Mood]: "Mood",
};

export interface PaginatedTagsResponse {
  tags: TagDto[];
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface CreateTagResponse {
  id: string;
  name: string;
  color: string;
  icon: string;
  description?: string | null;
  userId: string;
  createdAt: string;
}

export interface UpdateTagResponse {
  id: string;
  name: string;
  updatedAt: string;
}

export interface FileTagsResponse {
  fileId: string;
  tags: TagDto[];
}

export interface AddTagsResponse {
  fileId: string;
  tagsAdded: number;
  message: string;
}

export interface FileDto {
  id: string;
  name: string;
  path: string;
  mimeType: string;
  size: string;
  createdAt: string;
  hasPreview: boolean;
  tags: TagDto[];
}

export interface PaginatedFilesResponse {
  files: FileDto[];
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
export const tagApi = {
  // Create a new tag
  createTag: (data: CreateTagSchema): Promise<CreateTagResponse> =>
    api.post<CreateTagResponse>("/tags", data),

  // Get all tags with pagination
  getAllTags: (page: number, pageSize: number): Promise<PaginatedResponse<TagDto>> =>
    api.get<PaginatedResponse<TagDto>>("/tags", {
      params: { page, pageSize },
    }),

  // Update a tag
  updateTag: (tagId: string, data: UpdateTagSchema): Promise<UpdateTagResponse> =>
    api.patch<UpdateTagResponse>(`/tags/${tagId}`, data),

  // Delete a tag
  deleteTag: async (tagId: string): Promise<void> => {
    await api.delete(`/tags/${tagId}`);
  },

  // Search tags with filters
  searchTags: (query: SearchTagsSchema): Promise<PaginatedResponse<TagDto>> =>
    api.get<PaginatedResponse<TagDto>>("/tags/search", {
      params: query,
    }),

  // Load the full system vocabulary (pages until the server's total count is reached).
  getAllSystemTags: async (): Promise<TagDto[]> => {
    const pageSize = 100;
    const tags: TagDto[] = [];
    let page = 1;
    let total = 0;

    do {
      const response = await api.get<PaginatedResponse<TagDto>>("/tags/search", {
        params: { ownerScope: "system", page, pageSize },
      });
      total = response.totalCount;
      tags.push(...response.items);
      page++;
    } while (tags.length < total && page <= 20);

    return tags;
  },

  // Get all tags for a file
  getTagsForFile: (fileId: string): Promise<FileTagsResponse> =>
    api.get<FileTagsResponse>(`/files/${fileId}/tags`),

  // Add tags to a file
  addTagsToFile: (fileId: string, data: AddTagsToFileSchema): Promise<AddTagsResponse> =>
    api.post<AddTagsResponse>(`/files/${fileId}/tags`, data),

  // Remove a tag from a file
  removeTagFromFile: async (fileId: string, tagId: string): Promise<void> => {
    await api.delete(`/files/${fileId}/tags/${tagId}`);
  },

  // Search files by tags
  searchFilesByTags: (query: SearchFilesByTagsSchema): Promise<PaginatedResponse<TagDto>> =>
    api.get<PaginatedResponse<TagDto>>("/files/search/tags", {
      params: query,
    }),
};
