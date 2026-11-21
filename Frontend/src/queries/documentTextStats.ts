import { defineQueryOptions } from "@pinia/colada";

import { documentTextStatsApi } from "@/api/documentTextStats";

export const DOCUMENT_TEXT_STATS_ROOT = ["document-text-stats"] as const;

// The server sends no-store; a short client staleness keeps tab switches cheap.
export const documentTextOverview = defineQueryOptions(() => ({
  key: [...DOCUMENT_TEXT_STATS_ROOT, "overview"],
  query: () => documentTextStatsApi.getOverview(),
  staleTime: 15_000,
}));
