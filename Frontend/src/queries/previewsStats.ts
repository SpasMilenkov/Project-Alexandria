import { defineQueryOptions } from "@pinia/colada";

import type {
  PreviewJobTrendParams,
  PreviewJobType,
  PreviewVolumeParams,
} from "@/api/previewsStats";

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

export const previewsJobOverview = defineQueryOptions((type?: PreviewJobType) => ({
  key: [...ROOT, "job-overview", type ?? null],
  query: () => previewsStatsApi.getJobOverview(type),
  staleTime: 15_000,
}));

export const previewsJobTrend = defineQueryOptions((params: PreviewJobTrendParams) => ({
  key: [...ROOT, "job-trend", params.from, params.to, params.bucket, params.type ?? null],
  query: () => previewsStatsApi.getJobTrend(params),
  staleTime: 15_000,
}));
