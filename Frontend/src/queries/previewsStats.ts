import { defineQueryOptions } from "@pinia/colada";

import type { PreviewVolumeParams } from "@/api/previewsStats";

import { previewsStatsApi } from "@/api/previewsStats";

const ROOT = ["previews-stats"] as const;

// Server ResponseCache(5) — tight client staleness for a live dashboard
export const previewsOverview = defineQueryOptions(() => ({
  key: [...ROOT, "overview"],
  query: () => previewsStatsApi.getOverview(),
  staleTime: 15_000,
}));

export const previewsVolume = defineQueryOptions((params: PreviewVolumeParams) => ({
  key: [...ROOT, "volume", params.from, params.to, params.bucket],
  query: () => previewsStatsApi.getVolume(params),
  staleTime: 15_000,
}));
