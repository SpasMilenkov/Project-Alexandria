import { apiClient } from "./client";

// Backend enums serialize as numbers (no JsonStringEnumConverter)
export const PREVIEW_STATS_BUCKET = { Hour: 0, Day: 1 } as const;
export type PreviewStatsBucket = (typeof PREVIEW_STATS_BUCKET)[keyof typeof PREVIEW_STATS_BUCKET];

export interface PreviewKindTotals {
  kind: number; // 0 = Thumbnail, 1 = Preview
  count: number;
  totalSizeBytes: number;
}

export interface PreviewsOverviewResponse {
  byKind: PreviewKindTotals[];
}

export interface PreviewVolumePoint {
  bucketStart: string; // ISO
  thumbnails: number;
  previews: number;
}

export interface PreviewVolumeParams {
  from: string;
  to: string;
  bucket: PreviewStatsBucket;
}

// Backend JobType ordinals for the preview workers
export const PREVIEW_JOB_TYPE = { DocumentPreview: 1, MediaPreview: 2 } as const;
export type PreviewJobType = (typeof PREVIEW_JOB_TYPE)[keyof typeof PREVIEW_JOB_TYPE];

// Backend JobStatus ordinals (shared with the transpilation dashboard)
export const PREVIEW_JOB_STATUS = {
  Queued: 0,
  Processing: 1,
  Partial: 2,
  Ready: 3,
  Failed: 4,
  Cancelled: 5,
  CancellationRequested: 6,
} as const;

export interface JobStatusCount {
  status: number;
  count: number;
}

export interface PreviewJobDurationStats {
  avgMinutes: number;
  p50Minutes: number;
  p90Minutes: number;
  sampleCount: number;
}

export interface PreviewJobOverviewResponse {
  statusCounts: JobStatusCount[];
  duration: PreviewJobDurationStats | null;
}

export interface PreviewJobRatePoint {
  bucketStart: string; // ISO
  total: number;
  failed: number;
  failureRate: number;
}

export interface PreviewJobVolumePoint {
  bucketStart: string; // ISO
  count: number;
}

export interface PreviewJobTrendParams {
  from: string;
  to: string;
  bucket: PreviewStatsBucket;
  type?: PreviewJobType;
}

export interface PreviewJobTrendResponse {
  from: string;
  to: string;
  bucket: number;
  failureRate: PreviewJobRatePoint[];
  volume: PreviewJobVolumePoint[];
}

export interface PreviewVolumeResponse {
  from: string;
  to: string;
  bucket: number;
  points: PreviewVolumePoint[];
}

export const previewsStatsApi = {
  getOverview: async (): Promise<PreviewsOverviewResponse> => {
    const result = await apiClient.get<PreviewsOverviewResponse>("/admin/previews/overview");
    return result.data;
  },

  getVolume: async (params: PreviewVolumeParams): Promise<PreviewVolumeResponse> => {
    const result = await apiClient.get<PreviewVolumeResponse>("/admin/previews/volume", {
      params,
    });
    return result.data;
  },

  getJobOverview: async (type?: PreviewJobType): Promise<PreviewJobOverviewResponse> => {
    const result = await apiClient.get<PreviewJobOverviewResponse>("/admin/previews/job-overview", {
      params: type === undefined ? {} : { type },
    });
    return result.data;
  },

  getJobTrend: async (params: PreviewJobTrendParams): Promise<PreviewJobTrendResponse> => {
    const result = await apiClient.get<PreviewJobTrendResponse>("/admin/previews/job-trend", {
      params,
    });
    return result.data;
  },
};
