import { defineQueryOptions } from "@pinia/colada";

import type { StatsBucket, TranspilationTrendParams } from "@/api/transpilationStats";

import { transpilationStatsApi } from "@/api/transpilationStats";

const ROOT = ["transpilation-stats"] as const;

// Server ResponseCache(5) — keep client staleness tight for a live dashboard
export const transpilationOverview = defineQueryOptions(() => ({
  key: [...ROOT, "overview"],
  query: () => transpilationStatsApi.getOverview(),
  staleTime: 15_000,
}));

export const transpilationTrend = defineQueryOptions((params: TranspilationTrendParams) => ({
  key: [...ROOT, "trend", params.from, params.to, params.bucket],
  query: () => transpilationStatsApi.getTrend(params),
  staleTime: 15_000,
}));
