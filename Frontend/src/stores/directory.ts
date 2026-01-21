import { ref } from "vue";
import { acceptHMRUpdate, defineStore } from "pinia";
import {
  directoryApi,
  type SearchDirectoryRequest,
} from "@/api/directory";
import type { AxiosError } from "axios";

export const useDirectoryStore = defineStore(
  "directory",
  () => {
    // State
    const directoriesToCopy = ref<string[]>([]);
    const isLoading = ref(false);
    const isSearching = ref(false);
    const error = ref<string | null>(null);

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

    return {
      // State
      directoriesToCopy,
      isLoading,
      isSearching,
      error,
      // Actions
      searchDirectory,
      clearError,
    };
  },
  { persist: true }
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useDirectoryStore, import.meta.hot));
}
