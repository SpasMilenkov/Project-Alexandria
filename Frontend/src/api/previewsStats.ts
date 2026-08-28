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
};
