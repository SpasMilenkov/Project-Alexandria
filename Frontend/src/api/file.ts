import apiClient from "./client";
import type {
  UpdateFileMetadataSchema,
  GenerateSignedUrlSchema,
} from "@/schemas/file";
import type { FileSummary, PaginatedResponse } from "./directory";
import type { PaginationParams } from "@/types/pagination-params";

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
    id: string,
    fileName: string,
    mimeType: string,
    hasPreview: boolean
  },
  previewUrl: string,
  thumbnailUrl: string,
  textPreview: string,
  archivePreview: string
}

export interface FileVersionDto {
  id: string;
  size: string; // BigInteger serialized as string
  mimeType: string;
  versionNumber: number;
}

export interface TagDto {
  id: string;
  name: string;
  userId: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserDto {
  id: string;
  name: string;
  email: string;
}

export const fileApi = {
  // Upload a file
  uploadFile: async (formData: FormData): Promise<void> => {
    await apiClient.post("/files/upload", formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    });
  },

  // Download a file by ID
  downloadFile: async (id: string): Promise<Blob> => {
    const response = await apiClient.get(`/files/${id}`, {
      responseType: "blob",
    });
    return response.data;
  },

  getRootFiles: async (
    paginationParams: PaginationParams
  )=> {
    const response = await apiClient.get<PaginatedResponse<FileResult>>(
      "/files/root",
      {
        params: {
          page: paginationParams.page,
          pageSize: paginationParams.pageSize,
        },
      }
    );
    return{success: true, data: response.data};
  },

  getSubFiles: async(directoryId: string, paginationParams: PaginationParams): Promise<PaginatedResponse<FileResult>> => {
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
      }
    )
    return response.data
  },

  // Update file metadata
  updateFileMetadata: async (
    id: string,
    data: UpdateFileMetadataSchema
  ): Promise<UpdateFileMetadataResponse> => {
    const response = await apiClient.put<UpdateFileMetadataResponse>(
      `/files/${id}/metadata`,
      data
    );
    return response.data;
  },

  // Delete a file (soft delete)
  deleteFile: async (path: string): Promise<void> => {
    await apiClient.delete(`/files/${path}`);
  },

  // Generate signed URL for file upload/access
  generateSignedUrl: async (
    data: GenerateSignedUrlSchema
  ): Promise<GenerateSignedUrlResponse> => {
    const response = await apiClient.post<GenerateSignedUrlResponse>(
      "/files/signed-url",
      data
    );
    return response.data;
  },

  // Get file preview
  getPreview: async (id: string): Promise<PreviewResultDto> => {
    const response = await apiClient.get<PreviewResultDto>(`/files/${id}/preview`);
    return response.data;
  },

  // Get file thumbnail with specific dimensions
  getThumbnail: async (
    id: string,
    width: number,
    height: number
  ): Promise<Blob> => {
    const response = await apiClient.get(
      `/files/${id}/thumbnail/${width}/${height}`,
      {
        responseType: "blob",
      }
    );
    return response.data;
  },
};
