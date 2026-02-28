import { defineQueryOptions } from "@pinia/colada";

import { type AuditLogQuery, activityApi } from "@/api/activity";

export const ACTIVITY_QUERY_OPTIONS = {
  personalPaginated: (params: AuditLogQuery) => [
    ...ACTIVITY_QUERY_OPTIONS.root,
    params.page,
    params.pageSize,
  ],
  root: ["activities"] as const,
};

export const personalPaginated = defineQueryOptions((params: AuditLogQuery) => ({
  key: ACTIVITY_QUERY_OPTIONS.personalPaginated(params),
  placeholderData: (prev) => prev,
  query: () => activityApi.getUserActivity(params),
}));
