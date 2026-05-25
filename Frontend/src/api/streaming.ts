import type { TranspilationStatus } from "@/enums/transpilation-status";

import type { PaginatedResponse, UserDto } from "./directory";
import type { FileResult, FileVersionDto } from "./file";
import type { TagDto } from "./tag";

import { apiClient } from "./client";

export enum RepresentationStatus {
  Pending = 0,
  Processing = 1,
  Ready = 2,
  Failed = 3,
}

// Query / request param types

export interface GetFilesForStreamingQuery {
  query?: string | null;
  playlistId?: string | null;
  page: number;
  pageSize: number;
}

export interface TranspilationJobQuery {
  status?: TranspilationStatus;
  isVideo?: boolean;
  versionId?: string;
  createdAfter?: string;
  createdBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
  minRetryCount?: number;
  currentPage: number;
  pageSize: number;
}

export interface StreamHistoryQuery {
  fileId?: string;
  completed?: boolean;
  lastAccessedAfter?: string;
  lastAccessedBefore?: string;
  currentPage: number;
  pageSize: number;
}

export interface StartSessionRequest {
  fileId: string;
  startPositionSeconds: number;
}

export interface CloseSessionRequest {
  endPositionSeconds: number;
  listenedSeconds: number;
}

export interface StopTranspilationJobRequest {
  jobId: string;
  status: TranspilationStatus;
}

// Response types

export interface StreamHistoryResponse {
  id: string;
  fileId: string;
  positionSeconds: number;
  maxPositionReachedSeconds: number;
  totalListenedSeconds: number;
  timesCompleted: number;
  lastCompletedAt: string | null;
  lastAccessedAt: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface StreamSessionResponse {
  id: string;
  streamHistoryId: string;
  startPositionSeconds: number;
  endPositionSeconds: number;
  listenedSeconds: number;
  reachedCompletionThreshold: boolean;
  startedAt: string;
  endedAt: string | null;
}

export interface StreamingRepresentationResponse {
  id: string;
  jobId: string;
  codec: string;
  width?: number;
  height?: number;
  bitrateKbs?: number;
  status: RepresentationStatus;
  segmentPrefix?: string;
  completedAt?: string;
}

export interface TranspilationJobResponse {
  id: string;
  versionId: string;
  status: TranspilationStatus;
  isVideo: boolean;
  progressPercent: number;
  retryCount: number;
  errorDetail?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
  representations: StreamingRepresentationResponse[];
}

export interface RawMediaFileDto {
  file: FileResult;
  duration: number;
  artist: string | null;
  album: string | null;
  title: string | null;
  transpilationJobId: string;
  isVideo: boolean;
  segmentPrefix: string | null;
}

export interface MediaFileDto {
  // Hoisted from FileResult
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

  // Media metadata
  duration: number;
  artist: string | null;
  album: string | null;
  title: string | null;

  // Transpilation
  transpilationJobId: string;
  isVideo: boolean;
  segmentPrefix: string | null;
}

const mapMediaFileDto = (raw: RawMediaFileDto): MediaFileDto => ({
  fileId: raw.file.fileId,
  fileName: raw.file.fileName,
  mimeType: raw.file.mimeType,
  directoryId: raw.file.directoryId,
  createdAt: raw.file.createdAt,
  updatedAt: raw.file.updatedAt,
  deletedAt: raw.file.deletedAt,
  currentVersion: raw.file.currentVersion,
  tags: raw.file.tags,
  owner: raw.file.owner,
  duration: raw.duration,
  artist: raw.artist,
  album: raw.album,
  title: raw.title,
  transpilationJobId: raw.transpilationJobId,
  isVideo: raw.isVideo,
  segmentPrefix: raw.segmentPrefix,
});
// API

export const streamingApi = {
  getFilesForStreaming: async (
    query: GetFilesForStreamingQuery,
  ): Promise<PaginatedResponse<MediaFileDto>> => {
    const result = await apiClient.get<PaginatedResponse<RawMediaFileDto>>("/streaming/files", {
      params: query,
    });

    return { ...result.data, items: result.data.items.map((i) => mapMediaFileDto(i)) };
  },

  getManifest: async (id: string): Promise<string> => {
    const result = await apiClient.get<string>(`/streaming/manifest/${id}`);
    return result.data;
  },

  getHistory: async (
    query: StreamHistoryQuery,
  ): Promise<PaginatedResponse<StreamHistoryResponse>> => {
    const result = await apiClient.get<PaginatedResponse<StreamHistoryResponse>>(
      "/stream-history",
      { params: query },
    );
    return result.data;
  },

  getHistoryByFile: async (fileId: string): Promise<StreamHistoryResponse | null> => {
    const result = await apiClient.get<StreamHistoryResponse | null>("/stream-history/by-file", {
      params: { fileId },
    });
    return result.data;
  },

  getSessions: async (streamHistoryId: string): Promise<StreamSessionResponse[]> => {
    const result = await apiClient.get<StreamSessionResponse[]>(
      `/stream-history/${streamHistoryId}/sessions`,
    );
    return result.data;
  },

  startSession: async (req: StartSessionRequest): Promise<StreamSessionResponse> => {
    const result = await apiClient.post<StreamSessionResponse>("/stream-history/sessions", req);
    return result.data;
  },

  closeSession: async (
    sessionId: string,
    req: CloseSessionRequest,
  ): Promise<StreamHistoryResponse> => {
    const result = await apiClient.patch<StreamHistoryResponse>(
      `/stream-history/sessions/${sessionId}/close`,
      req,
    );
    return result.data;
  },

  getTranspilationJobs: async (
    query: TranspilationJobQuery,
  ): Promise<PaginatedResponse<TranspilationJobResponse>> => {
    const result = await apiClient.get<PaginatedResponse<TranspilationJobResponse>>(
      "/streaming/jobs",
      { params: query },
    );
    return result.data;
  },

  queueTranspilationJob: async (versionId: string): Promise<void> => {
    await apiClient.post("/streaming/job", { versionId });
  },

  stopTranspilationJob: async (req: StopTranspilationJobRequest): Promise<void> => {
    await apiClient.put("/streaming/stop-transpilation", req);
  },
};
