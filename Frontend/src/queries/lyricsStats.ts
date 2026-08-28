import { defineQueryOptions } from "@pinia/colada";

import type { LyricsTrendParams } from "@/api/lyricsStats";

import { lyricsStatsApi } from "@/api/lyricsStats";

const ROOT = ["lyrics-stats"] as const;

// Server ResponseCache(5) — tight client staleness for a live dashboard
export const lyricsOverview = defineQueryOptions(() => ({
  key: [...ROOT, "overview"],
  query: () => lyricsStatsApi.getOverview(),
  staleTime: 15_000,
}));

export const lyricsTrend = defineQueryOptions((params: LyricsTrendParams) => ({
  key: [...ROOT, "trend", params.from, params.to, params.bucket],
  query: () => lyricsStatsApi.getTrend(params),
  staleTime: 15_000,
}));
