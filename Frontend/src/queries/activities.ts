import { activityApi, type AuditLogQuery } from "@/api/activity";
import { defineQueryOptions } from "@pinia/colada";

export const ACTIVITY_QUERY_OPTIONS = {
  root: ["activities"] as const,
  personalPaginated: (params: AuditLogQuery) => [
    ...ACTIVITY_QUERY_OPTIONS.root,
    params.page,
    params.pageSize,
  ],
};

export const personalPaginated = defineQueryOptions(
  (params: AuditLogQuery) => ({
    key: ACTIVITY_QUERY_OPTIONS.personalPaginated(params),
    query: () => activityApi.getUserActivity(params),
    placeholderData: (prev) => prev,
  }),
);
