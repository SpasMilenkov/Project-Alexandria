import type { TranspilationStatus } from "@/enums/transpilation-status";

import type { PaginatedResponse } from "./directory";
import type { AudioRung, VideoRung } from "./policy";

import { apiClient } from "./client";

export enum RepresentationStatus {
  Pending = 0,
  Processing = 1,
  Ready = 2,
  Failed = 3,
}

export interface QueueTranspilationJobRequest {
  versionId: string;
  audioRungs: AudioRung[];
  videoRungs: VideoRung[];
}

// Query / request param types

export interface GetFilesForStreamingQuery {
  query?: string | null;
  playlistId?: string | null;
  page: number;
  pageSize: number;
  isVideo: boolean
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

export interface UpdateTranspilationJobRequest {
  jobId: string;
  status: TranspilationStatus;
  audioRungs?: AudioRung[];
  videoRungs?: VideoRung[];
}

// Response types

export interface StreamHistoryResponse {
  id: string;
  fileId: string;
  title: string;
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
  fileName?: string;
  versionNumber?: number;
  audioRungs: AudioRung[];
  videoRungs: VideoRung[];
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
  representations: StreamingRepresentationResponse[];
}

export interface MediaFileDto {
  // Hoisted from FileResult
  fileId: string;
  fileName: string;
  mimeType: string;
  currentVersionId: string;

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


export const streamingApi = {
  getFilesForStreaming: async (
    query: GetFilesForStreamingQuery,
  ): Promise<PaginatedResponse<MediaFileDto>> => {
    const result = await apiClient.get<PaginatedResponse<MediaFileDto>>("/streaming/files", {
      params: query,
    });

    return result.data;
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

  getSessions: async (streamHistoryId: string): Promise<PaginatedResponse<StreamSessionResponse>> => {
    const result = await apiClient.get<PaginatedResponse<StreamSessionResponse>>(
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

  queueTranspilationJob: async (params: QueueTranspilationJobRequest): Promise<void> => {
    await apiClient.post("/streaming/job", params);
  },

  updateTranspilationJob: async (req: UpdateTranspilationJobRequest): Promise<void> => {
    await apiClient.patch("/streaming/update-transpilation-status", req);
  },
};
