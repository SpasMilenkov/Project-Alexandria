import { defineQueryOptions } from "@pinia/colada";

import { outboxApi } from "@/api/outbox";

export const OUTBOX_QUERY_KEYS = {
  root: ["admin", "outbox"] as const,
  summary: () => [...OUTBOX_QUERY_KEYS.root, "summary"],
};

export const outboxSummary = defineQueryOptions(() => ({
  key: OUTBOX_QUERY_KEYS.summary(),
  placeholderData: (prev) => prev,
  query: () => outboxApi.getSummary(),
  staleTime: 15_000,
}));
