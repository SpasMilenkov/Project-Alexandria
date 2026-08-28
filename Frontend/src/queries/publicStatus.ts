import { defineQueryOptions } from "@pinia/colada";

import { publicStatusApi } from "@/api/publicStatus";

const ROOT = ["public-status"] as const;

export const publicCurrentStatus = defineQueryOptions(() => ({
  key: [...ROOT, "current"],
  query: () => publicStatusApi.getCurrentStatus(),
  staleTime: 30_000,
}));

// Server caches 60s and the page deliberately never polls — a status page that
// is slightly stale on an un-refreshed tab is the accepted tradeoff (D11).
export const publicStatusHistory = defineQueryOptions((days: number) => ({
  key: [...ROOT, "history", days],
  query: () => publicStatusApi.getStatusHistory(days),
  staleTime: 60_000,
}));
