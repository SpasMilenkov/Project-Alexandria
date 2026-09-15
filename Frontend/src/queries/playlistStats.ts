import { defineQueryOptions } from "@pinia/colada";

import { type PlaylistTrendParams, playlistStatsApi } from "@/api/playlistStats";

const ROOT = ["playlist-stats"] as const;

// Server ResponseCache(10) — keep client staleness tight for a live dashboard
export const playlistOverview = defineQueryOptions(() => ({
  key: [...ROOT, "overview"],
  query: () => playlistStatsApi.getOverview(),
  staleTime: 15_000,
}));

export const playlistTrend = defineQueryOptions((params: PlaylistTrendParams) => ({
  key: [...ROOT, "trend", params.from, params.to, params.bucket],
  query: () => playlistStatsApi.getTrend(params),
  staleTime: 15_000,
}));
