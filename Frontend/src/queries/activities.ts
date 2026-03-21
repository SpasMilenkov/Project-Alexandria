import { defineQueryOptions } from "@pinia/colada";

import { type ActivityQuery, type AuditLogQuery, activityApi } from "@/api/activity";

export const ACTIVITY_QUERY_OPTIONS = {
  personalPaginated: (params: AuditLogQuery) => [
    ...ACTIVITY_QUERY_OPTIONS.root,
    params.page,
    params.pageSize,
  ],
  root: ["activities"] as const,
  summary: (params: ActivityQuery) => [
    ...ACTIVITY_QUERY_OPTIONS.root,
    "summary",
    params.startDate,
    params.endDate,
    params.entityType,
    params.operationType,
  ],
};

export const personalPaginated = defineQueryOptions((params: AuditLogQuery) => ({
  key: ACTIVITY_QUERY_OPTIONS.personalPaginated(params),
  placeholderData: (prev) => prev,
  query: () => activityApi.getUserActivity(params),
}));

export const activitySummary = defineQueryOptions((params: ActivityQuery) => ({
  key: [
    ...ACTIVITY_QUERY_OPTIONS.root,
    "summary",
    params.startDate.toISOString(),
    params.endDate.toISOString(),
    params.entityType ?? null,
    params.operationType ?? null,
  ],
  query: () => activityApi.getActivitySummary(params),
  staleTime: 1000 * 60 * 60,
}));
