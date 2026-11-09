import type { TextPreviewTruncation } from "@/enums";
import type { GenerateSignedUrlSchema, UpdateFileMetadataSchema } from "@/schemas/file";
import type { FileSearchQuery } from "@/schemas/search";
import type { PaginationParams } from "@/types/pagination-params";

import { logger } from "@/utils/logger";

import type { PaginatedResponse } from "./directory";
import type { TagDto } from "./tag";

import { api } from "./client";
import { ApiError } from "./errors";

// Response Types
export interface UpdateFileMetadataResponse {
  id: string;
  name: string;
  title: string | null;
  artist: string | null;
  album: string | null;
  year: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

// The server requires both lists, so an empty one is sent rather than left out.
export interface InitBulkDownloadRequest {
  fileIds: string[];
  directoryIds: string[];
}

export interface GenerateSignedUrlResponse {
  url: string;
  expiresAt: string;
}

export interface AutoTagFileResponse {
  fileId: string;
  queued: boolean;
  message: string;
}

export interface BulkUpdateFileMetadataRequest {
  fileIds: string[];
  title?: string | null;
  artist?: string | null;
  album?: string | null;
  year?: string | null;
}

export interface BulkUpdateFileMetadataItemResult {
  fileId: string;
  success: boolean;
  error: string | null;
}

export interface BulkUpdateFileMetadataResponse {
  results: BulkUpdateFileMetadataItemResult[];
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

// thumbnailUrl removed, thumbnails are a separate endpoint/call now
// (Kind = Thumbnail is its own artifact, not bundled into the preview response)
export interface PreviewResultDto {
  previewUrl: string | null;
  textPreview: TextPreviewDto | null;
  archivePreview: string | null;
}

export interface TextPreviewToken {
  start: number;
  length: number;
  kind: string;
}

export interface TextPreviewLine {
  text: string;
  tokens: TextPreviewToken[];
}

export interface TextPreviewDto {
  lines: TextPreviewLine[];
  language: string;
  truncated: boolean;
  truncation: TextPreviewTruncation;
  highlightingLimited: boolean;
  bytesShown: number;
  totalBytes: number | null;
  encoding: string;
  lineCount: number;
  tokenCount: number;
  domElementCount: number;
}

export interface FileVersionDto {
  id: string;
  size: string;
  mimeType: string;
  versionNumber: number;
  isDeleted: boolean;
  isEncrypted: boolean;
  // The document itself needs a password to open, so the worker renders no preview for it
  isPasswordProtected: boolean;
  // Set when this version has a manual cover
  coverUpdatedAt?: string | null;
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
  iterationCount: number | null;
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

export interface InitializeFileUploadBatchResult {
  uploadId: string | null;
  uploadUrl: string | null;
  error: string | null;
}

export interface InitializeFileUploadBatchResponse {
  results: InitializeFileUploadBatchResult[];
}

export interface FinalizeFileUploadBatchResult {
  uploadId: string;
  success: boolean;
  error: string | null;
}

export interface FinalizeFileUploadBatchResponse {
  results: FinalizeFileUploadBatchResult[];
}

export interface CaptureMetadataDto {
  fileVersionId: string;
  /** False means not read yet or unreadable, so empty fields are unknown rather than absent. */
  isExtracted: boolean;
  hasLocation: boolean;
  width: number | null;
  height: number | null;
  /** A true instant only when capturedOffsetMinutes is set; otherwise the camera's wall clock. */
  capturedAt: string | null;
  capturedOffsetMinutes: number | null;
  latitude: number | null;
  longitude: number | null;
  cameraMake: string | null;
  cameraModel: string | null;
  lensModel: string | null;
  software: string | null;
  focalLengthMm: number | null;
  focalLength35mm: number | null;
  fNumber: number | null;
  exposureSeconds: number | null;
  iso: number | null;
}

export interface DocumentMetadataDto {
  fileVersionId: string;
  /** False means not read yet or unreadable, so empty fields are unknown rather than absent. */
  isExtracted: boolean;
  /** True when the file names a person or company that a downloaded copy still carries. */
  hasPersonalDetails: boolean;
  isPasswordProtected: boolean;
  /** An ebook whose pages a store's copy protection encrypts; its properties are still read. */
  isDrmProtected: boolean;
  title: string | null;
  subject: string | null;
  keywords: string | null;
  language: string | null;
  author: string | null;
  lastModifiedBy: string | null;
  company: string | null;
  application: string | null;
  producer: string | null;
  /** A true instant only when authoredOffsetMinutes is set; otherwise the author's wall clock. */
  authoredAt: string | null;
  authoredOffsetMinutes: number | null;
  modifiedAt: string | null;
  modifiedOffsetMinutes: number | null;
  pageCount: number | null;
  wordCount: number | null;
  slideCount: number | null;
  sheetCount: number | null;
  editingMinutes: number | null;
  paperSize: string | null;
  publisher: string | null;
  /** Always the 13-digit form. */
  isbn: string | null;
  series: string | null;
  seriesIndex: number | null;
}

// Verifying a batch re-reads every file, so this sits just under nginx's 300s proxy timeout.
const FINALIZE_BATCH_TIMEOUT_MS = 280_000;

export const fileApi = {
  bulkDownloadInit: async (payload: InitBulkDownloadRequest): Promise<string> => {
    const response = await api.post<{ token: string }>("files/bulk-download/init", payload);
    return response.token;
  },

  changeFileVersion: async ({
    fileId,
    versionId,
  }: {
    fileId: string;
    versionId: string;
  }): Promise<void> => {
    await api.patch("files/versions", { fileId, versionId });
  },

  copyFiles: async (fileIds: string[], destinationId: string | null) =>
    await api.post(`/files/copy`, {
      destinationId,
      fileIds,
    }),

  getCaptureMetadata: (fileId: string, versionId: string | null): Promise<CaptureMetadataDto> =>
    api.get<CaptureMetadataDto>(`/files/${fileId}/capture-metadata`, { params: { versionId } }),

  getDocumentMetadata: (fileId: string, versionId: string | null): Promise<DocumentMetadataDto> =>
    api.get<DocumentMetadataDto>(`/files/${fileId}/document-metadata`, { params: { versionId } }),

  deleteFileVersion: async (id: string): Promise<void> => {
    await api.delete(`/files/versions/${id}`);
  },

  deleteFiles: async (ids: string[], hardDelete?: boolean): Promise<void> => {
    await api.delete(`/files/`, {
      body: {
        hardDelete: hardDelete ?? false,
        ids,
      },
    });
  },

  autoTagFile: (fileId: string): Promise<AutoTagFileResponse> =>
    api.post<AutoTagFileResponse>(`/files/${fileId}/auto-tag`),

  downloadFile: (id: string): Promise<DownloadInfo> =>
    api.get<DownloadInfo>(`/files/download/${id}`),

  // Was: downloadFileVersion: async (id: string): Promise<string>
  downloadFileVersion: (id: string): Promise<DownloadInfo> =>
    api.get<DownloadInfo>(`files/versions/${id}`),

  finalizeUploadBatch: (
    items: FinalizeFileUploadRequest[],
  ): Promise<FinalizeFileUploadBatchResponse> =>
    api.post<FinalizeFileUploadBatchResponse>(
      "/files/finalize-upload/batch",
      { items },
      { timeout: FINALIZE_BATCH_TIMEOUT_MS },
    ),

  generateSignedUrl: (data: GenerateSignedUrlSchema): Promise<GenerateSignedUrlResponse> =>
    api.post<GenerateSignedUrlResponse>("/files/signed-url", data),

  getFile: (id: string): Promise<FileResult> => {
    logger.log("fetching file with id", id);
    return api.get<FileResult>(`/files/${id}`);
  },

  // Hits the unversioned redirect endpoint, resolves to the file's current version
  // server-side. Prefer getPreviewByVersion when the caller already has a versionId
  // (e.g. from a MediaFileDto/FileResult already in hand), it skips the redirect hop.
  getPreview: (fileId: string): Promise<PreviewResultDto> =>
    api.get<PreviewResultDto>(`/files/${fileId}/preview`),

  getPreviewByVersion: (fileId: string, versionId: string): Promise<PreviewResultDto> =>
    api.get<PreviewResultDto>(`/files/${fileId}/versions/${versionId}/preview`),

  getRootFiles: (paginationParams: PaginationParams) =>
    api.get<PaginatedResponse<FileResult>>("/files/root", {
      params: {
        page: paginationParams.page,
        pageSize: paginationParams.pageSize,
        sortBy: paginationParams.SortBy,
        sortDirection: paginationParams.sortDirection,
      },
    }),

  getSubFiles: (
    directoryId: string,
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<FileResult>> =>
    api.get<PaginatedResponse<FileResult>>(`files/directory/${directoryId}`, {
      params: {
        directoryId,
        page: paginationParams.page,
        pageSize: paginationParams.pageSize,
        sortBy: paginationParams.SortBy,
        sortDirection: paginationParams.sortDirection,
      },
    }),

  // URL builders, not fetch calls. Point <img :src> directly at these so the
  // browser's native HTTP cache (backed by nginx proxy_cache server-side) does
  // the work, no blob fetch, no JS-owned caching layer. The endpoint itself
  // redirects (302) to the presigned URL, browsers follow img-src redirects
  // transparently.
  getThumbnailUrl: (fileId: string): string => `/api/files/${fileId}/thumbnail`,

  // Version URLs are cached long-term, so a replaced manual cover needs a new URL.
  getThumbnailUrlForVersion: (
    fileId: string,
    versionId: string,
    coverUpdatedAt?: string | null,
  ): string => {
    const base = `/api/files/${fileId}/versions/${versionId}/thumbnail`;

    return coverUpdatedAt ? `${base}?v=${encodeURIComponent(coverUpdatedAt)}` : base;
  },

  uploadVersionCover: (
    fileId: string,
    versionId: string,
    file: File,
  ): Promise<{ coverUpdatedAt: string }> => {
    const form = new FormData();
    form.append("file", file);

    return api.post<{ coverUpdatedAt: string }>(
      `/files/${fileId}/versions/${versionId}/cover`,
      form,
    );
  },

  removeVersionCover: async (fileId: string, versionId: string): Promise<void> => {
    await api.delete(`/files/${fileId}/versions/${versionId}/cover`);
  },

  getVersionsForFile: ({
    id,
    page,
    pageSize,
  }: {
    id: string;
    page: number;
    pageSize: number;
  }): Promise<PaginatedResponse<FileVersionDto>> => {
    logger.log("searching for versions for file with id", id);
    return api.get<PaginatedResponse<FileVersionDto>>("/files/versions", {
      params: { fileId: id, page, pageSize },
    });
  },

  initializeUploadBatch: (
    items: InitializeFileUploadRequest[],
  ): Promise<InitializeFileUploadBatchResponse> =>
    api.post<InitializeFileUploadBatchResponse>("/files/init-upload/batch", { items }),

  moveFiles: async (fileIds: string[], destinationId: string | null) => {
    await api.post("/files/move", {
      destinationId,
      fileIds,
    });
  },

  restoreFiles: (fileIds: string[]): Promise<number> =>
    api.post<number>("/files/restore", {
      fileIds,
    }),

  restoreVersion: async (id: string) => {
    await api.patch(`/files/versions/restore/${id}`, {});
  },

  bulkUpdateFileMetadata: (
    data: BulkUpdateFileMetadataRequest,
  ): Promise<BulkUpdateFileMetadataResponse> =>
    api.patch<BulkUpdateFileMetadataResponse>("/files/metadata/bulk", data),

  searchFiles: (query: FileSearchQuery): Promise<PaginatedResponse<FileResult>> => {
    const cleanQuery = Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      Object.entries(query).filter(([_, value]) => value !== null),
    );
    return api.get<PaginatedResponse<FileResult>>("/files/search", {
      params: cleanQuery,
    });
  },

  updateFileMetadata: (data: UpdateFileMetadataSchema): Promise<UpdateFileMetadataResponse> =>
    api.patch<UpdateFileMetadataResponse>(`/files/${data.id}/metadata`, data),

  uploadToS3: (
    presignedUrl: string,
    file: Blob,
    contentType: string,
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
          reject(
            new ApiError(`Upload failed with status ${xhr.status}`, {
              kind: "http",
              status: xhr.status,
            }),
          );
        }
      });

      xhr.addEventListener("error", () => reject(new Error("Network error during upload")));
      xhr.addEventListener("abort", () =>
        reject(new DOMException("Upload cancelled", "AbortError")),
      );

      signal?.addEventListener("abort", () => xhr.abort(), { once: true });

      xhr.open("PUT", presignedUrl);
      xhr.setRequestHeader("Content-Type", contentType);
      xhr.send(file);
    }),
};
