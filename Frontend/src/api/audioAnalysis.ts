import { api } from "@/api/client";

export interface EnrichmentQueueDepthRow {
  backbone: string;
  // Linked JobStatus name (Queued/Processing/Partial/Ready/Failed/...).
  status: string;
  count: number;
}

export interface EnrichmentStuckRow {
  fileId: string;
  batchId: string;
  backbone: string;
  createdAt: string;
}

export interface EnrichmentFailureRatePoint {
  bucket: string;
  backbone: string;
  failed: number;
  succeeded: number;
  total: number;
  failureRate: number;
}

export interface EnrichmentDurationRow {
  backbone: string;
  averageSeconds: number;
  medianSeconds: number;
  maxSeconds: number;
}

export interface EnrichmentBatchRow {
  id: string;
  backbone: string;
  status: string;
  dispatchedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  filesCompleted: number;
  filesTotal: number;
}

export interface EnrichmentVolumePoint {
  hour: string;
  count: number;
}

export type EnrichmentBucket = "hour" | "day";

export interface EnrichmentRowDto {
  analyzer: string;
  version: string;
  payload: Record<string, unknown>;
  createdAt: string;
}

export interface EnrichmentBatchAttemptDto {
  batchId: string;
  batchStatus: string;
  // Linked JobStatus name (Queued/Processing/Partial/Ready/Failed/...).
  fileStatus: string;
  errorDetail: string | null;
  createdAt: string;
}

export interface GetFileAudioAnalysisResponse {
  fileId: string;
  enrichments: EnrichmentRowDto[];
  batches: EnrichmentBatchAttemptDto[];
}

export const audioAnalysisApi = {
  getQueueDepth: (): Promise<EnrichmentQueueDepthRow[]> =>
    api.get<EnrichmentQueueDepthRow[]>("/admin/enrichment/queue-depth"),

  getStuck: (): Promise<EnrichmentStuckRow[]> =>
    api.get<EnrichmentStuckRow[]>("/admin/enrichment/stuck"),

  getFailureRate: (
    from: string,
    to: string,
    bucket: EnrichmentBucket,
  ): Promise<EnrichmentFailureRatePoint[]> =>
    api.get<EnrichmentFailureRatePoint[]>("/admin/enrichment/failure-rate", {
      params: { from, to, bucket },
    }),

  getDuration: (): Promise<EnrichmentDurationRow[]> =>
    api.get<EnrichmentDurationRow[]>("/admin/enrichment/duration"),

  getBatches: (limit?: number): Promise<EnrichmentBatchRow[]> =>
    api.get<EnrichmentBatchRow[]>("/admin/enrichment/batches", {
      params: limit === undefined ? {} : { limit },
    }),

  getVolumeByHour: (from: string, to: string): Promise<EnrichmentVolumePoint[]> =>
    api.get<EnrichmentVolumePoint[]>("/admin/enrichment/volume-by-hour", {
      params: { from, to },
    }),

  getFileAudioAnalysis: (fileId: string): Promise<GetFileAudioAnalysisResponse> =>
    api.get<GetFileAudioAnalysisResponse>(`/files/${fileId}/enrichment`),
};
