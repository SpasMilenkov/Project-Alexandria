import type { AxiosError } from "axios";

import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { FileSearchQuery } from "@/schemas/search";

import { fileApi } from "@/api/file";
import { logger } from "@/utils/logger";

interface FileMetadata {
  id: string;
  name: string;
  hasPreview: boolean;
  previewGeneratedAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

interface DownloadFileParams {
  id: string;
  fileName?: string;
  forceDownload?: boolean;
}

export const useFileStore = defineStore("file", () => {
  // State
  const currentFile = ref<FileMetadata | null>(null);
  const modificationOriginDirId = ref<string | null>(null);
  const downloadProgress = ref<number>(0);
  const selectedFiles = ref<string[]>([]);
  const isUploading = ref(false);
  const isDownloading = ref(false);
  const error = ref<string | null>(null);

  // Getters
  const hasCurrentFile = computed(() => currentFile.value !== null);
  const isProcessing = computed(() => isUploading.value || isDownloading.value);

  // Actions

  // Download a file
  const downloadFile = async ({ id, fileName, forceDownload = false }: DownloadFileParams) => {
    isDownloading.value = true;
    downloadProgress.value = 0;
    error.value = null;

    try {
      const url = await fileApi.downloadFile(id);
      downloadProgress.value = 100;
      logger.log(url);
      if (forceDownload) {
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName ? fileName : `file-${id}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      } else {
        window.open(url, "_blank");
      }

      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to download file");
      return { error: message, success: false };
    } finally {
      isDownloading.value = false;
      downloadProgress.value = 0;
    }
  };
  const searchFiles = async (query: FileSearchQuery) => {
    try {
      const data = await fileApi.searchFiles(query);
      return { data, success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to download file");
      return { data: null, message, success: false };
    }
  };

  // Helper functions
  const handleError = (err: unknown, defaultMessage: string): string => {
    let message = defaultMessage;
    if (err instanceof Error) {
      ({ message } = err);
    }
    if ((err as AxiosError)?.response?.data) {
      message = (err as AxiosError<{ message: string }>).response?.data?.message ?? message;
    }
    error.value = message;
    return message;
  };

  const clearError = () => {
    error.value = null;
  };

  //oxlint-disable sort-keys
  return {
    // State
    currentFile,
    downloadProgress,
    selectedFiles,
    modificationOriginDirId,
    isUploading,
    isDownloading,
    error,
    // Getters
    hasCurrentFile,
    isProcessing,
    // Actions
    downloadFile,
    clearError,
    searchFiles,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useFileStore, import.meta.hot));
}
