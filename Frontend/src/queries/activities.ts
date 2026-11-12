import { defineQueryOptions } from "@pinia/colada";

import {
  type ActivityQuery,
  type AuditLogQuery,
  type EntityHistoryQuery,
  activityApi,
} from "@/api/activity";

export const ACTIVITY_QUERY_KEYS = {
  correlated: (correlationId: string) => [
    ...ACTIVITY_QUERY_KEYS.root,
    "correlation",
    correlationId,
  ],
  entityHistory: (query: EntityHistoryQuery) => [
    ...ACTIVITY_QUERY_KEYS.root,
    "entity",
    query.entityType,
    query.entityId,
    query.page,
    query.pageSize,
  ],
  // The owner is part of the key so one account never reads another's cached page.
  personalPaginated: (userId: string, query: AuditLogQuery) => [
    ...ACTIVITY_QUERY_KEYS.root,
    "personal",
    userId,
    query.page,
    query.pageSize,
    query.entityType ?? null,
    query.entityId ?? null,
    query.operationType ?? null,
    query.source ?? null,
    query.ipAddress ?? null,
    query.after ?? null,
    query.before ?? null,
  ],
  root: ["activities"] as const,
  security: (userId: string) => [...ACTIVITY_QUERY_KEYS.root, "security", userId],
  summary: (userId: string, query: ActivityQuery) => [
    ...ACTIVITY_QUERY_KEYS.root,
    "summary",
    userId,
    query.startDate.toISOString(),
    query.endDate.toISOString(),
    query.entityType ?? null,
    query.operationType ?? null,
  ],
};

export const personalPaginated = defineQueryOptions(
  ({ userId, query }: { userId: string; query: AuditLogQuery }) => ({
    enabled: Boolean(userId),
    key: ACTIVITY_QUERY_KEYS.personalPaginated(userId, query),
    placeholderData: (prev) => prev,
    query: () => activityApi.getUserActivity(query),
  }),
);

export const activitySummary = defineQueryOptions(
  ({ userId, query }: { userId: string; query: ActivityQuery }) => ({
    enabled: Boolean(userId),
    key: ACTIVITY_QUERY_KEYS.summary(userId, query),
    placeholderData: (prev) => prev,
    query: () => activityApi.getActivitySummary(query),
    staleTime: 1000 * 60 * 5,
  }),
);

export const entityHistory = defineQueryOptions((query: EntityHistoryQuery) => ({
  enabled: Boolean(query.entityId),
  key: ACTIVITY_QUERY_KEYS.entityHistory(query),
  placeholderData: (prev) => prev,
  query: () => activityApi.getEntityHistory(query),
}));

export const correlatedActivity = defineQueryOptions((correlationId: string) => ({
  enabled: Boolean(correlationId),
  key: ACTIVITY_QUERY_KEYS.correlated(correlationId),
  query: () => activityApi.getCorrelatedActivity(correlationId),
  // A request's rows never change once it has finished.
  staleTime: 1000 * 60 * 10,
}));

export const securitySummary = defineQueryOptions((userId: string) => ({
  enabled: Boolean(userId),
  key: ACTIVITY_QUERY_KEYS.security(userId),
  query: () => activityApi.getSecuritySummary(),
  staleTime: 30_000,
}));
