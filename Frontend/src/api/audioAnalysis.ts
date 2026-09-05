import { apiClient } from "@/api/client";

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
  getQueueDepth: async (): Promise<EnrichmentQueueDepthRow[]> => {
    const result = await apiClient.get<EnrichmentQueueDepthRow[]>("/admin/enrichment/queue-depth");
    return result.data;
  },

  getStuck: async (): Promise<EnrichmentStuckRow[]> => {
    const result = await apiClient.get<EnrichmentStuckRow[]>("/admin/enrichment/stuck");
    return result.data;
  },

  getFailureRate: async (
    from: string,
    to: string,
    bucket: EnrichmentBucket,
  ): Promise<EnrichmentFailureRatePoint[]> => {
    const result = await apiClient.get<EnrichmentFailureRatePoint[]>(
      "/admin/enrichment/failure-rate",
      {
        params: { from, to, bucket },
      },
    );
    return result.data;
  },

  getDuration: async (): Promise<EnrichmentDurationRow[]> => {
    const result = await apiClient.get<EnrichmentDurationRow[]>("/admin/enrichment/duration");
    return result.data;
  },

  getBatches: async (limit?: number): Promise<EnrichmentBatchRow[]> => {
    const result = await apiClient.get<EnrichmentBatchRow[]>("/admin/enrichment/batches", {
      params: limit === undefined ? {} : { limit },
    });
    return result.data;
  },

  getVolumeByHour: async (from: string, to: string): Promise<EnrichmentVolumePoint[]> => {
    const result = await apiClient.get<EnrichmentVolumePoint[]>(
      "/admin/enrichment/volume-by-hour",
      {
        params: { from, to },
      },
    );
    return result.data;
  },

  getFileAudioAnalysis: async (fileId: string): Promise<GetFileAudioAnalysisResponse> => {
    const result = await apiClient.get<GetFileAudioAnalysisResponse>(`/files/${fileId}/enrichment`);
    return result.data;
  },
};
