import { ref, computed } from "vue";
import { acceptHMRUpdate, defineStore } from "pinia";
import {
  tagApi,
  type TagDto,
  type PaginatedTagsResponse,
  type FileDto,
  type PaginatedFilesResponse,
} from "@/api/tag";
import type {
  CreateTagSchema,
  UpdateTagSchema,
  SearchTagsSchema,
  AddTagsToFileSchema,
  SearchFilesByTagsSchema,
} from "@/schemas/tag";
import type { AxiosError } from "axios";

export const useTagStore = defineStore("tag", () => {
  // State
  const tags = ref<TagDto[]>([]);
  const currentTag = ref<TagDto | null>(null);
  const fileTags = ref<Record<string, TagDto[]>>({});
  const searchResults = ref<TagDto[]>([]);
  const fileSearchResults = ref<FileDto[]>([]);
  const pagination = ref<{
    currentPage: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPrevious: boolean;
    hasNext: boolean;
  }>({
    currentPage: 0,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
    hasPrevious: false,
    hasNext: false,
  });
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  // Getters
  const tagCount = computed(() => tags.value.length);
  const hasMorePages = computed(() => pagination.value.hasNext);

  // Actions

  // Create a new tag
  const createTag = async (data: CreateTagSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.createTag(data);
      tags.value.unshift({
        ...response,
        updatedAt: null,
      });
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to create tag");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Get all tags with pagination
  const getAllTags = async (page: number = 0, pageSize: number = 20) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.getAllTags(page, pageSize);
      tags.value = response.tags;
      updatePagination(response);
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to fetch tags");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Update a tag
  const updateTag = async (tagId: string, data: UpdateTagSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.updateTag(tagId, data);
      const index = tags.value.findIndex((t) => t.id === tagId);
      if (index !== -1) {
        tags.value[index] = {
          ...tags.value[index],
          name: response.name,
          updatedAt: response.updatedAt,
        };
      }
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to update tag");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Delete a tag
  const deleteTag = async (tagId: string) => {
    isLoading.value = true;
    error.value = null;
    try {
      await tagApi.deleteTag(tagId);
      tags.value = tags.value.filter((t) => t.id !== tagId);
      // Remove from file tags cache
      Object.keys(fileTags.value).forEach((fileId) => {
        fileTags.value[fileId] = fileTags.value[fileId].filter(
          (t) => t.id !== tagId,
        );
      });
      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to delete tag");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Search tags with filters
  const searchTags = async (filters: SearchTagsSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.searchTags(filters);
      searchResults.value = response.tags;
      updatePagination(response);
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to search tags");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Get all tags for a file
  const getTagsForFile = async (fileId: string) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.getTagsForFile(fileId);
      fileTags.value[fileId] = response.tags;
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to fetch file tags");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Add tags to a file
  const addTagsToFile = async (fileId: string, data: AddTagsToFileSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.addTagsToFile(fileId, data);
      // Refresh file tags
      await getTagsForFile(fileId);
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to add tags to file");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Remove a tag from a file
  const removeTagFromFile = async (fileId: string, tagId: string) => {
    isLoading.value = true;
    error.value = null;
    try {
      await tagApi.removeTagFromFile(fileId, tagId);
      // Update cache
      if (fileTags.value[fileId]) {
        fileTags.value[fileId] = fileTags.value[fileId].filter(
          (t) => t.id !== tagId,
        );
      }
      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to remove tag from file");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Search files by tags
  const searchFilesByTags = async (filters: SearchFilesByTagsSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await tagApi.searchFilesByTags(filters);
      fileSearchResults.value = response.files;
      updatePagination(response);
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to search files by tags");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Helper functions
  const updatePagination = (
    response: PaginatedTagsResponse | PaginatedFilesResponse,
  ) => {
    pagination.value = {
      currentPage: response.currentPage,
      pageSize: response.pageSize,
      totalCount: response.totalCount,
      totalPages: response.totalPages,
      hasPrevious: response.hasPrevious,
      hasNext: response.hasNext,
    };
  };

  const handleError = (err: unknown, defaultMessage: string): string => {
    let message = defaultMessage;
    if (err instanceof Error) {
      message = err.message;
    }
    if ((err as AxiosError)?.response?.data) {
      message =
        (err as AxiosError<{ message: string }>).response?.data?.message ??
        message;
    }
    error.value = message;
    return message;
  };

  const clearError = () => {
    error.value = null;
  };

  const clearSearchResults = () => {
    searchResults.value = [];
    fileSearchResults.value = [];
  };

  return {
    // State
    tags,
    currentTag,
    fileTags,
    searchResults,
    fileSearchResults,
    pagination,
    isLoading,
    error,
    // Getters
    tagCount,
    hasMorePages,
    // Actions
    createTag,
    getAllTags,
    updateTag,
    deleteTag,
    searchTags,
    getTagsForFile,
    addTagsToFile,
    removeTagFromFile,
    searchFilesByTags,
    clearError,
    clearSearchResults,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useTagStore, import.meta.hot));
}
