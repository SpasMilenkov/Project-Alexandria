import { defineQueryOptions } from "@pinia/colada";

import {
  type GetFilesForStreamingQuery,
  type StreamHistoryQuery,
  type TranspilationJobQuery,
  streamingApi,
} from "@/api/streaming";

export const STREAMING_QUERY_KEYS = {
  root: ["streaming"] as const,
  manifest: (id: string) => [...STREAMING_QUERY_KEYS.root, "manifest", id],
  filesForStreaming: (query: GetFilesForStreamingQuery) => [
    ...STREAMING_QUERY_KEYS.root,
    "files-for-streaming",
    query.playlistId ?? null,
    query.page,
    query.pageSize,
    query.isVideo,
    query.query ?? null,
  ],

  history: (query: StreamHistoryQuery) => [
    ...STREAMING_QUERY_KEYS.root,
    "history",
    query.fileId ?? null,
    query.completed ?? null,
    query.lastAccessedAfter ?? null,
    query.lastAccessedBefore ?? null,
    query.currentPage,
    query.pageSize,
  ],
  historyByFile: (fileId: string) => [...STREAMING_QUERY_KEYS.root, "history", "by-file", fileId],
  sessions: (streamHistoryId: string) => [
    ...STREAMING_QUERY_KEYS.root,
    "sessions",
    streamHistoryId,
  ],
  transpilationJobs: (query: TranspilationJobQuery) => [
    ...STREAMING_QUERY_KEYS.root,
    "transpilation-jobs",
    query.status ?? null,
    query.isVideo ?? null,
    query.versionId ?? null,
    query.createdAfter ?? null,
    query.createdBefore ?? null,
    query.completedAfter ?? null,
    query.completedBefore ?? null,
    query.minRetryCount ?? null,
    query.currentPage,
    query.pageSize,
  ],
};

export const getManifest = defineQueryOptions((id: string) => ({
  key: STREAMING_QUERY_KEYS.manifest(id),
  query: () => streamingApi.getManifest(id),
  staleTime: 60_000,
}));

export const getFilesForStreaming = defineQueryOptions((query: GetFilesForStreamingQuery) => ({
  key: STREAMING_QUERY_KEYS.filesForStreaming(query),
  placeholderData: (prev) => prev,
  query: () => streamingApi.getFilesForStreaming(query),
}));

export const getHistory = defineQueryOptions((query: StreamHistoryQuery) => ({
  key: STREAMING_QUERY_KEYS.history(query),
  placeholderData: (prev) => prev,
  query: () => streamingApi.getHistory(query),
}));

export const getHistoryByFile = defineQueryOptions((fileId: string) => ({
  key: STREAMING_QUERY_KEYS.historyByFile(fileId),
  query: () => streamingApi.getHistoryByFile(fileId),
  staleTime: 5_000,
}));

export const getSessions = defineQueryOptions((streamHistoryId: string) => ({
  key: STREAMING_QUERY_KEYS.sessions(streamHistoryId),
  query: () => streamingApi.getSessions(streamHistoryId),
}));

export const getTranspilationJobs = defineQueryOptions((query: TranspilationJobQuery) => ({
  key: STREAMING_QUERY_KEYS.transpilationJobs(query),
  placeholderData: (prev) => prev,
  query: () => streamingApi.getTranspilationJobs(query),
}));
