import { apiClient } from "./client";

export const PLAYLIST_STATUS = {
  Queued: 0,
  Processing: 1,
  Partial: 2,
  Ready: 3,
  Failed: 4,
  Cancelled: 5,
  CancellationRequested: 6,
} as const;

export const PLAYLIST_BUCKET = { Hour: 0, Day: 1 } as const;
export type PlaylistBucket = (typeof PLAYLIST_BUCKET)[keyof typeof PLAYLIST_BUCKET];

export interface JobStatusCount {
  status: number;
  count: number;
}

export interface PlaylistRatePoint {
  bucketStart: string; // ISO
  total: number;
  failed: number;
  failureRate: number;
}

export interface PlaylistVolumePoint {
  bucketStart: string; // ISO
  count: number;
}

export interface PlaylistDurationStats {
  avgMinutes: number;
  p50Minutes: number;
  p90Minutes: number;
  sampleCount: number;
}

export interface PlaylistOverviewResponse {
  statusCounts: JobStatusCount[];
  duration: PlaylistDurationStats | null;
}

export interface PlaylistTrendParams {
  from: string;
  to: string;
  bucket: PlaylistBucket;
}

export interface PlaylistTrendResponse {
  from: string;
  to: string;
  bucket: number;
  failureRate: PlaylistRatePoint[];
  volume: PlaylistVolumePoint[];
}

export const playlistStatsApi = {
  getOverview: async (): Promise<PlaylistOverviewResponse> => {
    const result = await apiClient.get<PlaylistOverviewResponse>("/admin/playlists/overview");
    return result.data;
  },

  getTrend: async (params: PlaylistTrendParams): Promise<PlaylistTrendResponse> => {
    const result = await apiClient.get<PlaylistTrendResponse>("/admin/playlists/trend", {
      params,
    });
    return result.data;
  },
};
