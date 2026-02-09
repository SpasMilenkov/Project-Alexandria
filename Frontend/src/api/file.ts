import apiClient from "./client";
import type {
  UpdateFileMetadataSchema,
  GenerateSignedUrlSchema,
} from "@/schemas/file";
import type { PaginatedResponse } from "./directory";
import type { PaginationParams } from "@/types/pagination-params";
import type { TagDto } from "./tag";

// Response Types
export interface UpdateFileMetadataResponse {
  id: string;
  name: string;
  hasPreview: boolean;
  previewGeneratedAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface GenerateSignedUrlResponse {
  url: string;
  expiresAt: string;
}

export interface FileResult {
  fileId: string;
  fileName: string;
  mimeType: string;
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
  size: string; // BigInteger serialized as string
  mimeType: string;
  versionNumber: number;
}

export interface UserDto {
  id: string;
  name: string;
  email: string;
}

// New upload flow types
export interface InitializeFileUploadRequest {
  contentType: string;
  sha256: string;
  contentLength: number;
  directoryId?: string | null;
}

export interface InitializeFileUploadResponse {
  uploadId: string;
  uploadUrl: string;
}

export interface FinalizeFileUploadRequest {
  uploadId: string;
  directoryId?: string | null;
  fileName: string;
}

export interface FinalizeFileUploadResponse {
  success: boolean;
  fileId?: string;
}

export const fileApi = {
  // Initialize file upload - get presigned URL
  initializeUpload: async (
    data: InitializeFileUploadRequest,
  ): Promise<InitializeFileUploadResponse> => {
    const response = await apiClient.post<InitializeFileUploadResponse>(
      "/files/init-upload",
      data,
    );
    return response.data;
  },

  // Upload file directly to S3 using presigned URL
  uploadToS3: async (
    presignedUrl: string,
    file: File,
    onProgress?: (percent: number) => void,
  ): Promise<void> => {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      // Track upload progress
      xhr.upload.addEventListener("progress", (e) => {
        if (e.lengthComputable && onProgress) {
          const percent = (e.loaded / e.total) * 100;
          onProgress(percent);
        }
      });

      // Handle completion
      xhr.addEventListener("load", () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve();
        } else {
          reject(new Error(`Upload failed with status ${xhr.status}`));
        }
      });

      // Handle errors
      xhr.addEventListener("error", () => {
        reject(new Error("Network error during upload"));
      });

      xhr.addEventListener("abort", () => {
        reject(new Error("Upload aborted"));
      });

      // Initiate upload
      xhr.open("PUT", presignedUrl);
      xhr.setRequestHeader("Content-Type", file.type || "application/octet-stream"  );
      xhr.send(file);
    });
  },

  // Finalize upload
  finalizeUpload: async (
    data: FinalizeFileUploadRequest,
  ): Promise<FinalizeFileUploadResponse> => {
    const response = await apiClient.post<FinalizeFileUploadResponse>(
      "/files/finalize-upload",
      data,
    );
    return response.data;
  },

  // Legacy upload method (kept for backwards compatibility if needed)
  uploadFile: async (formData: FormData): Promise<void> => {
    await apiClient.post("/files/upload", formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    });
  },

  // Download a file by ID
  downloadFile: async (id: string): Promise<string> => {
    const response = await apiClient.get<string>(`/files/${id}`);
    return response.data;
  },

  getRootFiles: async (paginationParams: PaginationParams) => {
    const response = await apiClient.get<PaginatedResponse<FileResult>>(
      "/files/root",
      {
        params: {
          page: paginationParams.page,
          pageSize: paginationParams.pageSize,
          sortBy: paginationParams.orderBy,
          sortDirection: paginationParams.sortDirection,
        },
      },
    );
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
          sortBy: paginationParams.orderBy,
          sortDirection: paginationParams.sortDirection,
        },
      },
    );
    return response.data;
  },

  // Update file metadata
  updateFileMetadata: async (
    id: string,
    data: UpdateFileMetadataSchema,
  ): Promise<UpdateFileMetadataResponse> => {
    const response = await apiClient.put<UpdateFileMetadataResponse>(
      `/files/${id}/metadata`,
      data,
    );
    return response.data;
  },

  // Delete a file (soft delete)
  deleteFiles: async (ids: string[]): Promise<void> => {
    await apiClient.delete(`/files/`, {
      data: {
        ids,
      },
    });
  },

  // Generate signed URL for file upload/access
  generateSignedUrl: async (
    data: GenerateSignedUrlSchema,
  ): Promise<GenerateSignedUrlResponse> => {
    const response = await apiClient.post<GenerateSignedUrlResponse>(
      "/files/signed-url",
      data,
    );
    return response.data;
  },

  // Get file preview
  getPreview: async (id: string): Promise<PreviewResultDto> => {
    const response = await apiClient.get<PreviewResultDto>(
      `/files/${id}/preview`,
    );
    return response.data;
  },

  copyFiles: async (fileIds: string[], destinationId: string | null) =>
    await apiClient.post(`/files/copy`, {
      fileIds,
      destinationId,
    }),

  moveFiles: async (fileIds: string[], destinationId: string) => {
    await apiClient.post("/files/move", {
      fileIds,
      destinationId,
    });
  },

  // Get file thumbnail with specific dimensions
  getThumbnail: async (
    id: string,
    width: number,
    height: number,
  ): Promise<Blob> => {
    const response = await apiClient.get(
      `/files/${id}/thumbnail/${width}/${height}`,
      {
        responseType: "blob",
      },
    );
    return response.data;
  },
};