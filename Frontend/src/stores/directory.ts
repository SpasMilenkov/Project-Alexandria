import { ref, computed } from "vue";
import { acceptHMRUpdate, defineStore } from "pinia";
import {
  directoryApi,
  type DirectoryDto,
  type DirectorySummaryDto,
  type SearchDirectoryRequest,
} from "@/api/directory";
import type {
  CreateDirectorySchema,
  UpdateDirectorySchema,
  DeleteDirectorySchema,
} from "@/schemas/directory";
import type { AxiosError } from "axios";
import type { PaginationParams } from "@/types/pagination-params";

export const useDirectoryStore = defineStore(
  "directory",
  () => {
    // State
    const directories = ref<DirectorySummaryDto[]>([]);
    const currentDirectory = ref<DirectoryDto | null>(null);
    const rootDirectories = ref<string[]>([]);
    const directoriesToCopy = ref<string[]>([]);
    const directoryCache = ref<Record<string, DirectoryDto>>({});
    const isLoading = ref(false);
    const isSearching = ref(false);
    const error = ref<string | null>(null);
    const selectedItems = ref<Set<string>>(new Set());
    const selectionType = ref<"file" | "directory" | "mixed">("mixed");
    const pathList = ref<{ id: string; name: string }[]>([]);
    const navigationHistory = ref<Array<string | null>>([]);
    // Getters
    const directoryCount = computed(() => directories.value.length);
    const hasCurrentDirectory = computed(() => currentDirectory.value !== null);
    const currentDirectoryFiles = computed(
      () => currentDirectory.value?.files ?? []
    );
    const currentDirectorySubdirs = computed(
      () => currentDirectory.value?.directories ?? []
    );
    // Actions

    // Create a new directory
    const createDirectory = async (data: CreateDirectorySchema) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await directoryApi.createDirectory(data);
        directories.value.push(response.directory);
        // Update parent directory cache if it exists
        if (data.parentId && directoryCache.value[data.parentId]) {
          directoryCache.value[data.parentId].directories.push(
            response.directory
          );
        }
        return { success: true, data: response.directory };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to create directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    // Get directory by ID
    const getDirectory = async (directoryId: string) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await directoryApi.getDirectory(directoryId);
        const index = directories.value.findIndex((d) => d.id === directoryId);
        if (index !== -1) {
          directories.value[index] = response.directory;
        } else {
          directories.value.push(response.directory);
        }
        return { success: true, data: response.directory };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to fetch directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const getRooSubDirectories = async (paginationParams: PaginationParams) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response =
          await directoryApi.getRooSubDirectories(paginationParams);
        console.log("store response", response);
        pathList.value = [];
        return { success: true, data: response };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to fetch root directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const getDirectoryPath = async (directoryId: string) => {
      try {
        const response = await directoryApi.getDirectoryPath(directoryId);
        pathList.value = response.pathParts; // Updates the store's pathList
        return { success: true, data: response.pathParts };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to fetch directory path");
        return { success: false, error: message };
      }
    };

    const getSubDirectories = async (
      directoryId: string,
      paginationParams: PaginationParams
    ) => {
      isLoading.value = true;
      error.value = null;
      try {
        const result = await directoryApi.getSubDirectories(
          directoryId,
          paginationParams
        );

        return { success: true, data: result };
      } catch (err) {
        const message = handleError(
          err,
          "Failed to fetch directory with children"
        );
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const searchDirectory = async (req: SearchDirectoryRequest) => {
      isSearching.value = true;
      error.value = null;
      try {
        const response = await directoryApi.searchDirectory(req);

        return { success: true, data: response };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to find directory");
        return { success: false, error: message };
      } finally {
        isSearching.value = false;
      }
    };

    // Update directory
    const updateDirectory = async (data: UpdateDirectorySchema) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await directoryApi.updateDirectory(data);
        // Update in directories list
        const index = directories.value.findIndex(
          (d) => d.id === data.directoryId
        );
        if (index !== -1) {
          directories.value[index] = {
            id: response.directory.id,
            name: response.directory.name,
            parentId: response.directory.parentId,
            createdAt: response.directory.createdAt,
            updatedAt: response.directory.updatedAt,
            ownerUserDto: response.directory.ownerUserDto,
          };
        }
        console.log("updatedDir:", response);
        // Update in cache
        if (directoryCache.value[data.directoryId]) {
          directoryCache.value[data.directoryId] = response.directory;
        }
        // Update current directory if it matches
        if (currentDirectory.value?.id === data.directoryId) {
          currentDirectory.value = response.directory;
        }
        return { success: true, data: response.directory };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to update directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    // Move directory
    const moveDirectories = async (
      directoryIds: string[],
      destinationId: string
    ) => {
      isLoading.value = true;
      error.value = null;
      try {
        await directoryApi.moveDirectories( directoryIds, destinationId );
        return { success: true };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to move directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const copyDirectory = async (
      directoryId: string,
      destinationId: string
    ) => {
      isLoading.value = true;
      error.value = null;
      try {
        await directoryApi.copyDirectory(directoryId, destinationId);
      } catch (err: unknown) {
        const message = handleError(err, "Failed to copy selected directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    // Delete directory
    const deleteDirectory = async (
      id: string,
      options: DeleteDirectorySchema = { force: false }
    ) => {
      isLoading.value = true;
      error.value = null;
      try {
        await directoryApi.deleteDirectory(id, options);
        // Remove from directories list
        directories.value = directories.value.filter((d) => d.id !== id);
        // Remove from cache
        delete directoryCache.value[id];
        // Clear current directory if it matches
        if (currentDirectory.value?.id === id) {
          currentDirectory.value = null;
        }
        // Remove from parent's subdirectories
        Object.values(directoryCache.value).forEach((dir) => {
          dir.directories = dir.directories.filter(
            (subdir) => subdir.id !== id
          );
        });
        return { success: true };
      } catch (err: unknown) {
        const message = handleError(err, "Failed to delete directory");
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const toggleSelection = (id: string, type: "file" | "directory") => {
      if (selectedItems.value.has(id)) {
        selectedItems.value.delete(id);
      } else {
        selectedItems.value.add(id);
      }
      selectedItems.value = new Set(selectedItems.value); // Trigger reactivity
    };

    const setSelection = (ids: string[]) => {
      selectedItems.value = new Set(ids);
    };

    const clearSelection = () => {
      selectedItems.value.clear();
      selectedItems.value = new Set(); // Trigger reactivity
    };

    const isSelected = (id: string) => selectedItems.value.has(id);

    // Helper functions
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

    const clearCurrentDirectory = () => {
      currentDirectory.value = null;
    };

    const clearCache = () => {
      directoryCache.value = {};
    };

    return {
      // State
      directories,
      currentDirectory,
      rootDirectories,
      directoryCache,
      directoriesToCopy,
      isLoading,
      isSearching,
      error,
      selectedItems,
      selectionType,
      pathList,
      navigationHistory,
      // Getters
      directoryCount,
      hasCurrentDirectory,
      currentDirectoryFiles,
      currentDirectorySubdirs,
      // Actions
      createDirectory,
      getDirectory,
      getRooSubDirectories,
      getSubDirectories,
      getDirectoryPath,
      searchDirectory,
      updateDirectory,
      moveDirectories,
      copyDirectory,
      deleteDirectory,
      clearError,
      clearCurrentDirectory,
      clearCache,
      toggleSelection,
      setSelection,
      clearSelection,
      isSelected,
    };
  },
  { persist: true }
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useDirectoryStore, import.meta.hot));
}
