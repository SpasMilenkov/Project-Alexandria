import { api } from "./client";

// Backend enums serialize as numbers (no JsonStringEnumConverter)
export const STATS_BUCKET = { Hour: 0, Day: 1 } as const;
export type StatsBucket = (typeof STATS_BUCKET)[keyof typeof STATS_BUCKET];

// TranspilationStatus ordinals
export const TRANSPILATION_STATUS = {
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

export interface TranspilationRatePoint {
  bucketStart: string; // ISO
  total: number;
  failed: number;
  failureRate: number;
}

export interface TranspilationVolumePoint {
  bucketStart: string; // ISO
  count: number;
}

export interface TranspilationDurationStats {
  avgMinutes: number;
  p50Minutes: number;
  p90Minutes: number;
  sampleCount: number;
}

export interface TranspilationOverviewResponse {
  statusCounts: JobStatusCount[];
  duration: TranspilationDurationStats | null;
}

export interface TranspilationTrendParams {
  from: string;
  to: string;
  bucket: StatsBucket;
}

export interface TranspilationTrendResponse {
  from: string;
  to: string;
  bucket: number;
  failureRate: TranspilationRatePoint[];
  volume: TranspilationVolumePoint[];
}

export const transpilationStatsApi = {
  getOverview: (): Promise<TranspilationOverviewResponse> =>
    api.get<TranspilationOverviewResponse>("/admin/transpilation/overview"),

  getTrend: (params: TranspilationTrendParams): Promise<TranspilationTrendResponse> =>
    api.get<TranspilationTrendResponse>("/admin/transpilation/trend", {
      params,
    }),
};
