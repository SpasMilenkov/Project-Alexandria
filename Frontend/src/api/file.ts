import type { GenerateSignedUrlSchema, UpdateFileMetadataSchema } from "@/schemas/file";
import type { FileSearchQuery } from "@/schemas/search";
import type { PaginationParams } from "@/types/pagination-params";

import { logger } from "@/utils/logger";

import type { PaginatedResponse } from "./directory";
import type { TagDto } from "./tag";

import apiClient from "./client";

// Response Types
export interface UpdateFileMetadataResponse {
  id: string;
  name: string;
  hasPreview: boolean;
  previewGeneratedAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface InitBulkDownloadRequest {
  fileIds?: string[] | null;
  directoryIds?: string[] | null;
}

export interface GenerateSignedUrlResponse {
  url: string;
  expiresAt: string;
}

export interface FileResult {
  fileId: string;
  fileName: string;
  mimeType: string;
  directoryId: string | null;
  createdAt: string;
  updatedAt: string | null;
  deletedAt: string | null;
  currentVersion: FileVersionDto;
  tags: TagDto[];
  owner: UserDto;
}

export interface PreviewResultDto {
  metaData: {
    id: string;
    fileName: string;
    mimeType: string;
    hasPreview: boolean;
  };
  previewUrl: string;
  thumbnailUrl: string;
  textPreview: string;
  archivePreview: string;
}

export interface FileVersionDto {
  id: string;
  size: string;
  mimeType: string;
  versionNumber: number;
  isDeleted: boolean;
  isEncrypted: boolean;
}

export interface UserDto {
  id: string;
  name: string;
  email: string;
}

// New upload flow types
export interface InitializeFileUploadRequest {
  contentType: string;
  hash: string;
  contentLength: number;
  directoryId?: string | null;
}

export interface InitializeFileUploadResponse {
  uploadId: string;
  uploadUrl: string;
}

export interface DownloadInfo {
  presignedUrl: string;
  fileName: string;
  mimeType: string;
  isEncrypted: boolean;
  encryptionIv: string | null;
  encryptionSalt: string | null;
  integrityTag: string | null;
  encryptionHint: string | null;
}

export interface FinalizeFileUploadRequest {
  uploadId: string;
  directoryId?: string | null;
  fileName: string;
  isEncrypted: boolean;
  encryptionIv?: string | null;
  encryptionSalt?: string | null;
  integrityTag?: string | null;
  encryptionHint?: string | null;
  iterationCount?: number | null;
}

export interface FinalizeFileUploadResponse {
  success: boolean;
  fileId?: string;
}

export const fileApi = {
  bulkDownloadInit: async (payload: InitBulkDownloadRequest): Promise<string> => {
    const response = await apiClient.post<{ token: string }>("files/bulk-download/init", payload);
    return response.data.token;
  },

  changeFileVersion: async ({
    fileId,
    versionId,
  }: {
    fileId: string;
    versionId: string;
  }): Promise<void> => {
    await apiClient.patch("files/versions", { fileId, versionId });
  },

  copyFiles: async (fileIds: string[], destinationId: string | null) =>
    await apiClient.post(`/files/copy`, {
      destinationId,
      fileIds,
    }),

  deleteFileVersion: async (id: string): Promise<void> => {
    await apiClient.delete(`/files/versions/${id}`);
  },

  deleteFiles: async (ids: string[], hardDelete?: boolean): Promise<void> => {
    await apiClient.delete(`/files/`, {
      data: {
        hardDelete: hardDelete ?? false,
        ids,
      },
    });
  },

  downloadFile: async (id: string): Promise<DownloadInfo> => {
    const response = await apiClient.get<DownloadInfo>(`/files/download/${id}`);
    return response.data;
  },

  // Was: downloadFileVersion: async (id: string): Promise<string>
  downloadFileVersion: async (id: string): Promise<DownloadInfo> => {
    const response = await apiClient.get<DownloadInfo>(`files/versions/${id}`);
    return response.data;
  },

  finalizeUpload: async (data: FinalizeFileUploadRequest): Promise<FinalizeFileUploadResponse> => {
    const response = await apiClient.post<FinalizeFileUploadResponse>(
      "/files/finalize-upload",
      data,
    );
    return response.data;
  },

  generateSignedUrl: async (data: GenerateSignedUrlSchema): Promise<GenerateSignedUrlResponse> => {
    const response = await apiClient.post<GenerateSignedUrlResponse>("/files/signed-url", data);
    return response.data;
  },

  getFile: async (id: string): Promise<FileResult> => {
    logger.log("fetching file with id", id);
    const response = await apiClient.get<FileResult>(`/files/${id}`);
    return response.data;
  },

  getPreview: async (id: string): Promise<PreviewResultDto> => {
    const response = await apiClient.get<PreviewResultDto>(`/files/${id}/preview`);
    return response.data;
  },

  getRootFiles: async (paginationParams: PaginationParams) => {
    const response = await apiClient.get<PaginatedResponse<FileResult>>("/files/root", {
      params: {
        page: paginationParams.page,
        pageSize: paginationParams.pageSize,
        sortBy: paginationParams.SortBy,
        sortDirection: paginationParams.sortDirection,
      },
    });
    return response.data;
  },

  getSubFiles: async (
    directoryId: string,
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<FileResult>> => {
    const response = await apiClient.get<PaginatedResponse<FileResult>>(
      `files/directory/${directoryId}`,
      {
        params: {
          directoryId,
          page: paginationParams.page,
          pageSize: paginationParams.pageSize,
          sortBy: paginationParams.SortBy,
          sortDirection: paginationParams.sortDirection,
        },
      },
    );
    return response.data;
  },

  getThumbnail: async (id: string, width: number, height: number): Promise<Blob> => {
    const response = await apiClient.get(`/files/${id}/thumbnail/${width}/${height}`, {
      responseType: "blob",
    });
    return response.data;
  },

  getVersionsForFile: async ({
    id,
    page,
    pageSize,
  }: {
    id: string;
    page: number;
    pageSize: number;
  }): Promise<PaginatedResponse<FileVersionDto>> => {
    logger.log("searching for versions for file with id", id);
    const result = await apiClient.get<PaginatedResponse<FileVersionDto>>("/files/versions", {
      params: { fileId: id, page, pageSize },
    });
    return result.data;
  },

  initializeUpload: async (
    data: InitializeFileUploadRequest,
  ): Promise<InitializeFileUploadResponse> => {
    const response = await apiClient.post<InitializeFileUploadResponse>("/files/init-upload", data);
    return response.data;
  },

  moveFiles: async (fileIds: string[], destinationId: string | null) => {
    await apiClient.post("/files/move", {
      destinationId,
      fileIds,
    });
  },

  restoreFiles: async (fileIds: string[]): Promise<number> => {
    const result = await apiClient.post<number>("/files/restore", {
      fileIds,
    });
    return result.data;
  },

  restoreVersion: async (id: string) => {
    await apiClient.patch(`/files/versions/restore/${id}`, {});
  },

  searchFiles: async (query: FileSearchQuery): Promise<PaginatedResponse<FileResult>> => {
    const cleanQuery = Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      Object.entries(query).filter(([_, value]) => value !== null),
    );
    const response = await apiClient.get<PaginatedResponse<FileResult>>("/files/search", {
      params: cleanQuery,
    });
    return response.data;
  },

  updateFileMetadata: async (
    data: UpdateFileMetadataSchema,
  ): Promise<UpdateFileMetadataResponse> => {
    const response = await apiClient.patch<UpdateFileMetadataResponse>(
      `/files/${data.id}/metadata`,
      data,
    );
    return response.data;
  },

  uploadToS3: (
    presignedUrl: string,
    file: File,
    onProgress?: (percent: number) => void,
    signal?: AbortSignal,
  ): Promise<void> =>
    new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      xhr.upload.addEventListener("progress", (e) => {
        if (e.lengthComputable && onProgress) {
          onProgress((e.loaded / e.total) * 100);
        }
      });

      xhr.addEventListener("load", () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve();
        } else {
          reject(new Error(`Upload failed with status ${xhr.status}`));
        }
      });

      xhr.addEventListener("error", () => reject(new Error("Network error during upload")));
      xhr.addEventListener("abort", () =>
        reject(new DOMException("Upload cancelled", "AbortError")),
      );

      signal?.addEventListener("abort", () => xhr.abort(), { once: true });

      xhr.open("PUT", presignedUrl);
      xhr.setRequestHeader("Content-Type", file.type || "application/octet-stream");
      xhr.send(file);
    }),
};
