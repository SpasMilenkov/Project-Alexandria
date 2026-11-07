import type { JobStatus } from "@/enums/job-status";

import type { PaginatedResponse } from "./directory";
import type { AudioRung, VideoRung } from "./policy";

import { api } from "./client";

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
  isVideo: boolean;
  anchorFileId?: string | null;
  anchorPlaylistItemId?: string | null;
}

export interface TranspilationJobQuery {
  status?: JobStatus;
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
  qualified?: boolean;
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
  playbackFinished?: boolean;
}

export interface UpdateTranspilationJobRequest {
  jobId: string;
  status: JobStatus;
  audioRungs?: AudioRung[];
  videoRungs?: VideoRung[];
}

// Response types

export interface StreamHistoryResponse {
  id: string;
  fileId: string;
  title: string;
  artist?: string | null;
  album?: string | null;
  duration?: number | null;
  isVideo?: boolean | null;
  positionSeconds: number;
  maxPositionReachedSeconds: number;
  totalListenedSeconds: number;
  qualifiedPlayCount: number;
  lastPlayedAt: string | null;
  hasFinished: boolean;
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
  isQualifiedPlay: boolean;
  playbackFinished: boolean;
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
  status: JobStatus;
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
  playbackVersionId?: string;
  // Set when the current version has a manual cover
  coverUpdatedAt?: string | null;

  // Media metadata
  duration: number | null;
  artist: string | null;
  album: string | null;
  title: string | null;
  genre: string | null;
  year: string | null;

  // Transpilation
  transpilationJobId: string;
  playlistItemId?: string | null;
  isVideo: boolean;
  segmentPrefix: string | null;

  // Requesting user's progress, null without a history row
  positionSeconds?: number | null;
  hasFinished?: boolean | null;
  lastAccessedAt?: string | null;
  createdAt?: string | null;

  // Search results only: the lyric line that matched when the hit came from lyrics
  lyricsSnippet?: string | null;
}

export const streamingApi = {
  getFilesForStreaming: (
    query: GetFilesForStreamingQuery,
  ): Promise<PaginatedResponse<MediaFileDto>> =>
    api.get<PaginatedResponse<MediaFileDto>>("/streaming/files", {
      params: query,
    }),

  getStreamingFile: async (fileId: string): Promise<MediaFileDto | null> => {
    try {
      return await api.get<MediaFileDto>(`/streaming/files/${fileId}`);
    } catch (error: any) {
      if (error?.status === 404) return null;
      throw error;
    }
  },

  getManifest: (id: string): Promise<string> => api.get<string>(`/streaming/manifest/${id}`),

  getHistory: (query: StreamHistoryQuery): Promise<PaginatedResponse<StreamHistoryResponse>> =>
    api.get<PaginatedResponse<StreamHistoryResponse>>("/stream-history", { params: query }),

  getHistoryByFile: (fileId: string): Promise<StreamHistoryResponse | null> =>
    api.get<StreamHistoryResponse | null>("/stream-history/by-file", {
      params: { fileId },
    }),

  getSessions: (streamHistoryId: string): Promise<PaginatedResponse<StreamSessionResponse>> =>
    api.get<PaginatedResponse<StreamSessionResponse>>(
      `/stream-history/${streamHistoryId}/sessions`,
    ),

  startSession: (req: StartSessionRequest): Promise<StreamSessionResponse> =>
    api.post<StreamSessionResponse>("/stream-history/sessions", req),

  closeSession: (sessionId: string, req: CloseSessionRequest): Promise<StreamHistoryResponse> =>
    api.patch<StreamHistoryResponse>(`/stream-history/sessions/${sessionId}/close`, req),

  getTranspilationJobs: (
    query: TranspilationJobQuery,
  ): Promise<PaginatedResponse<TranspilationJobResponse>> =>
    api.get<PaginatedResponse<TranspilationJobResponse>>("/streaming/jobs", {
      params: query,
    }),

  queueTranspilationJob: async (params: QueueTranspilationJobRequest): Promise<void> => {
    await api.post("/streaming/job", params);
  },

  updateTranspilationJob: async (req: UpdateTranspilationJobRequest): Promise<void> => {
    await api.patch("/streaming/update-transpilation-status", req);
  },
};
