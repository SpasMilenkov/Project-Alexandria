import { defineQueryOptions } from "@pinia/colada";

import { type AdminAuditQuery, adminAuditApi } from "@/api/adminAudit";
import { userApi } from "@/api/users";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";

export const ADMIN_AUDIT_QUERY_KEYS = {
  root: ["admin-audit"] as const,
  overview: (days: number) => [...ADMIN_AUDIT_QUERY_KEYS.root, "overview", days],
  search: (query: AdminAuditQuery) => [
    ...ADMIN_AUDIT_QUERY_KEYS.root,
    "search",
    query.page,
    query.pageSize,
    query.userId ?? null,
    query.ownerId ?? null,
    query.entityType ?? null,
    query.entityId ?? null,
    (query.eventCodes ?? []).join(","),
    query.operationType ?? null,
    query.source ?? null,
    query.ipAddress ?? null,
    query.from ?? null,
    query.to ?? null,
    query.correlationId ?? null,
  ],
};

export const adminAuditSearch = defineQueryOptions((query: AdminAuditQuery) => ({
  key: ADMIN_AUDIT_QUERY_KEYS.search(query),
  placeholderData: (prev) => prev,
  query: () => adminAuditApi.searchAudit(query),
}));

export const adminAccessOverview = defineQueryOptions((days: number) => ({
  key: ADMIN_AUDIT_QUERY_KEYS.overview(days),
  placeholderData: (prev) => prev,
  query: () => adminAuditApi.getAccessOverview(days),
}));

// Local-network scale: one page of accounts covers the actor and owner pickers. The users
// endpoint counts pages from zero, unlike the audit endpoints.
export const auditUserOptions = defineQueryOptions(() => ({
  key: [...ADMIN_AUDIT_QUERY_KEYS.root, "users"],
  query: () =>
    userApi.getUsers({
      page: 0,
      pageSize: 100,
      showDeleted: true,
      showDeletedOnly: false,
      sortBy: SortBy.Name,
      sortDirection: SortDirection.Asc,
    }),
  staleTime: 5 * 60_000,
}));
