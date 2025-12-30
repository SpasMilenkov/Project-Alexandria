import { ref, computed } from "vue";
import { acceptHMRUpdate, defineStore } from "pinia";
import { fileApi } from "@/api/file";
import type {
  UpdateFileMetadataSchema,
  GenerateSignedUrlSchema,
} from "@/schemas/file";
import type { AxiosError } from "axios";
import type { PaginationParams } from "@/types/pagination-params";

interface FileMetadata {
  id: string;
  name: string;
  hasPreview: boolean;
  previewGeneratedAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}
type DownloadFileParams = {
  id: string;
  fileName?: string;
  forceDownload?: boolean;
};

export const useFileStore = defineStore("file", () => {
  // State
  const files = ref<FileMetadata[]>([]);
  const currentFile = ref<FileMetadata | null>(null);
  const uploadProgress = ref<number>(0);
  const downloadProgress = ref<number>(0);
  const signedUrls = ref<Record<string, { url: string; expiresAt: string }>>(
    {}
  );
  const isLoading = ref(false);
  const isUploading = ref(false);
  const isDownloading = ref(false);
  const error = ref<string | null>(null);

  // Getters
  const fileCount = computed(() => files.value.length);
  const hasCurrentFile = computed(() => currentFile.value !== null);
  const isProcessing = computed(
    () => isLoading.value || isUploading.value || isDownloading.value
  );

  // Actions

  // Upload a file
  const uploadFile = async (file: File, path?: string) => {
    isUploading.value = true;
    uploadProgress.value = 0;
    error.value = null;
    try {
      const formData = new FormData();
      formData.append("file", file);
      if (path) {
        formData.append("path", path);
      }

      await fileApi.uploadFile(formData);
      uploadProgress.value = 100;
      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to upload file");
      return { success: false, error: message };
    } finally {
      isUploading.value = false;
      uploadProgress.value = 0;
    }
  };

  const getRootFiles = async (paginationParams: PaginationParams) => {
    isLoading.value = true;
    error.value = null;
    try {
      const result = await fileApi.getRootFiles(paginationParams);

      return { success: true, data: result.data };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to load root files");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  const getSubFiles = async (
    directoryId: string,
    paginationParams: PaginationParams
  ) => {
    isLoading.value = true;
    error.value = null;
    try {
      const result = await fileApi.getSubFiles(directoryId, paginationParams);

      return { success: true, data: result };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to load sub files");
      return { success: false, err: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Download a file
  const downloadFile = async ({
    id,
    fileName,
    forceDownload = false,
  }: DownloadFileParams) => {
    isDownloading.value = true;
    downloadProgress.value = 0;
    error.value = null;

    try {
      const blob = await fileApi.downloadFile(id);
      downloadProgress.value = 100;

      const url = window.URL.createObjectURL(blob);

      if (forceDownload) {
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName ? fileName : `file-${id}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      } else {
        window.open(url, "_blank");
        setTimeout(() => {
          window.URL.revokeObjectURL(url);
        }, 100);
      }

      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to download file");
      return { success: false, error: message };
    } finally {
      isDownloading.value = false;
      downloadProgress.value = 0;
    }
  };

  // Update file metadata
  const updateFileMetadata = async (
    id: string,
    data: UpdateFileMetadataSchema
  ) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await fileApi.updateFileMetadata(id, data);
      // Update in files list
      const index = files.value.findIndex((f) => f.id === id);
      if (index !== -1) {
        files.value[index] = response;
      } else {
        files.value.push(response);
      }
      // Update current file if it matches
      if (currentFile.value?.id === id) {
        currentFile.value = response;
      }
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to update file metadata");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Delete a file
  const deleteFile = async (path: string) => {
    isLoading.value = true;
    error.value = null;
    try {
      await fileApi.deleteFile(path);
      // Remove from files list by path (would need to track path in state)
      // For now, we'll just clear the files list - in production you'd want better tracking
      return { success: true };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to delete file");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  // Generate signed URL
  const generateSignedUrl = async (data: GenerateSignedUrlSchema) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await fileApi.generateSignedUrl(data);
      signedUrls.value[data.name] = response;
      return { success: true, data: response };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to generate signed URL");
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

  const getFilePreview = async (id: string) => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await fileApi.getPreview(id)
      console.log('store response', response)
      return {success: true, data: response}
    } catch (err: unknown) {
      const message = handleError(err, "Failed to fetch preview")
      return {success: false, error: message}
    }
    finally{
      isLoading.value = false
    }
  }

  // Set current file
  const setCurrentFile = (file: FileMetadata | null) => {
    currentFile.value = file;
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


  const clearSignedUrls = () => {
    signedUrls.value = {};
  };

  return {
    // State
    files,
    currentFile,
    uploadProgress,
    downloadProgress,
    signedUrls,
    isLoading,
    isUploading,
    isDownloading,
    error,
    // Getters
    fileCount,
    hasCurrentFile,
    isProcessing,
    // Actions
    uploadFile,
    getRootFiles,
    getSubFiles,
    downloadFile,
    updateFileMetadata,
    deleteFile,
    getFilePreview,
    generateSignedUrl,
    setCurrentFile,
    clearError,
    clearSignedUrls,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useFileStore, import.meta.hot));
}
