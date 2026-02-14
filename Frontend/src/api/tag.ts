import apiClient from "./client";
import type {
  CreateTagSchema,
  UpdateTagSchema,
  SearchTagsSchema,
  AddTagsToFileSchema,
  SearchFilesByTagsSchema,
} from "@/schemas/tag";
import type { PaginatedResponse } from "./directory";

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
}

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
  createTag: async (data: CreateTagSchema): Promise<CreateTagResponse> => {
    const response = await apiClient.post<CreateTagResponse>("/tags", data);
    return response.data;
  },

  // Get all tags with pagination
  getAllTags: async (
    page: number,
    pageSize: number,
  ): Promise<PaginatedResponse<TagDto>> => {
    const response = await apiClient.get<PaginatedResponse<TagDto>>("/tags", {
      params: { page, pageSize },
    });
    return response.data;
  },

  // Update a tag
  updateTag: async (
    tagId: string,
    data: UpdateTagSchema,
  ): Promise<UpdateTagResponse> => {
    const response = await apiClient.patch<UpdateTagResponse>(
      `/tags/${tagId}`,
      data,
    );
    return response.data;
  },

  // Delete a tag
  deleteTag: async (tagId: string): Promise<void> => {
    await apiClient.delete(`/tags/${tagId}`);
  },

  // Search tags with filters
  searchTags: async (
    filters: SearchTagsSchema,
  ): Promise<PaginatedResponse<TagDto>> => {
    const response = await apiClient.post<PaginatedResponse<TagDto>>(
      "/tags/search",
      filters,
    );
    return response.data;
  },

  // Get all tags for a file
  getTagsForFile: async (fileId: string): Promise<FileTagsResponse> => {
    const response = await apiClient.get<FileTagsResponse>(
      `/files/${fileId}/tags`,
    );
    return response.data;
  },

  // Add tags to a file
  addTagsToFile: async (
    fileId: string,
    data: AddTagsToFileSchema,
  ): Promise<AddTagsResponse> => {
    const response = await apiClient.post<AddTagsResponse>(
      `/files/${fileId}/tags`,
      data,
    );
    return response.data;
  },

  // Remove a tag from a file
  removeTagFromFile: async (fileId: string, tagId: string): Promise<void> => {
    await apiClient.delete(`/files/${fileId}/tags/${tagId}`);
  },

  // Search files by tags
  searchFilesByTags: async (
    filters: SearchFilesByTagsSchema,
  ): Promise<PaginatedResponse<TagDto>> => {
    const response = await apiClient.post<PaginatedResponse<TagDto>>(
      "/files/search/tags",
      filters,
    );
    return response.data;
  },
};
