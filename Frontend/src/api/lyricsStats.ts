import { apiClient } from "./client";

// Backend enums serialize as numbers (no JsonStringEnumConverter)
export const LYRICS_STATS_BUCKET = { Hour: 0, Day: 1 } as const;
export type LyricsStatsBucket = (typeof LYRICS_STATS_BUCKET)[keyof typeof LYRICS_STATS_BUCKET];

export const LYRICS_STATUS = {
  PendingFetch: 0,
  Fetching: 1,
  Fetched: 2,
  FetchFailed: 3,
  NoMatch: 4,
} as const;

export const LYRICS_PROVIDER = {
  None: 0,
  LrclibPublic: 1,
  LrclibLocal: 2,
  Musicxmatch: 3,
  Manual: 4,
} as const;

export interface LyricsStatusCount {
  status: number;
  count: number;
}

export interface LyricsRatePoint {
  bucketStart: string; // ISO
  total: number;
  fetched: number;
  failed: number;
}

export interface LyricsProviderBreakdownRow {
  provider: number;
  total: number;
  failed: number;
}

export interface LyricsTrendParams {
  from: string;
  to: string;
  bucket: LyricsStatsBucket;
}

export interface LyricsTrendResponse {
  from: string;
  to: string;
  bucket: number;
  points: LyricsRatePoint[];
}

export interface LyricsOverviewResponse {
  statusCounts: LyricsStatusCount[];
  providers: LyricsProviderBreakdownRow[];
  fetchedTotal: number;
  avgConfidence: number | null;
}

export const lyricsStatsApi = {
  getOverview: async (): Promise<LyricsOverviewResponse> => {
    const result = await apiClient.get<LyricsOverviewResponse>("/admin/lyrics/overview");
    return result.data;
  },

  getTrend: async (params: LyricsTrendParams): Promise<LyricsTrendResponse> => {
    const result = await apiClient.get<LyricsTrendResponse>("/admin/lyrics/trend", { params });
    return result.data;
  },
};
