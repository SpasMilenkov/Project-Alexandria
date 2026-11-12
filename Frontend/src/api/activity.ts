import type { AuditEventCode } from "@/enums/audit-event-code";
import type { AuditExportFormat } from "@/enums/audit-export-format";
import type { AuditSortBy } from "@/enums/audit-sort-by";
import type { EntityType } from "@/enums/entity-type";
import type { LogSource } from "@/enums/log-source";
import type { OperationType } from "@/enums/operation-type";
import type { SortDirection } from "@/enums/SortDirection";

import type { PaginatedResponse } from "./directory";

import { api } from "./client";

export interface ActivityQuery {
  startDate: Date;
  endDate: Date;
  entityType?: EntityType;
  operationType?: OperationType;
}

export interface ActivitySummary {
  totalOperations: number;
  countPerOperation: Partial<Record<OperationType, number>>;
}

export interface ActivityStatisticsOverview {
  totalActivity: number;
  // Keyed by UTC day, "yyyy-MM-dd".
  activityPerDay: Record<string, ActivitySummary>;
}

export interface AuditLogResult {
  id: string;
  operationType: OperationType;
  entityType: EntityType;
  userId: string;
  // Actor display name; null for the system user or a deleted account.
  userName: string | null;
  entityId: string;
  ownerId: string | null;
  parentEntityId: string | null;
  correlationId: string | null;
  entityName: string | null;
  eventCode: AuditEventCode;
  fallbackDescription: string | null;
  ipAddress: string | null;
  metadata: string | null;
  timestamp: string;
  logSource: LogSource;
}

// The caller's own history. The backend always scopes it to the signed-in user.
export interface AuditLogQuery {
  page: number;
  pageSize: number;
  sortBy?: AuditSortBy;
  sortDirection?: SortDirection;

  entityId?: string;
  operationType?: OperationType;
  entityType?: EntityType;
  source?: LogSource;
  ipAddress?: string;
  before?: string;
  after?: string;
}

// Filters shared by the exports and the admin explorer.
export interface AuditFilterRequest {
  entityType?: EntityType;
  entityId?: string;
  eventCodes?: AuditEventCode[];
  operationType?: OperationType;
  source?: LogSource;
  ipAddress?: string;
  from?: string;
  to?: string;
  correlationId?: string;
}

// Row positions bounding one file of a split export, as the plan hands them out.
export interface AuditExportBounds {
  startAt: string;
  startId: string;
  endAt: string | null;
  endId: string | null;
}

export interface AuditExportPart extends AuditExportBounds {
  index: number;
  rows: number;
  oldestAt: string;
}

export interface AuditExportPlan {
  total: number;
  partSize: number;
  parts: AuditExportPart[];
}

export interface EntityHistoryQuery {
  entityType: EntityType;
  entityId: string;
  page: number;
  pageSize: number;
}

export interface KnownIpAddress {
  ipAddress: string;
  loginCount: number;
  firstSeen: string;
  lastSeen: string;
}

export interface SecuritySummary {
  recentLogins: AuditLogResult[];
  recentFailedAttempts: AuditLogResult[];
  failedAttemptsLast30Days: number;
  knownIpAddresses: KnownIpAddress[];
}

export const activityApi = {
  exportActivity: (
    filters: AuditFilterRequest,
    format: AuditExportFormat,
    bounds?: AuditExportBounds,
  ): Promise<Blob> =>
    api.get<Blob>("/activity/export", {
      params: { ...filters, ...bounds, format },
      responseType: "blob",
      // Exports stream up to the server's row cap, which can take a while on a big history.
      timeout: 300_000,
    }),

  getActivitySummary: (query: ActivityQuery): Promise<ActivityStatisticsOverview> =>
    api.get<ActivityStatisticsOverview>("/activity/summary", {
      params: {
        endDate: query.endDate,
        entityType: query.entityType,
        operationType: query.operationType,
        startDate: query.startDate,
      },
    }),

  getCorrelatedActivity: (correlationId: string): Promise<AuditLogResult[]> =>
    api.get<AuditLogResult[]>(`/activity/correlation/${correlationId}`),

  getEntityHistory: (query: EntityHistoryQuery): Promise<PaginatedResponse<AuditLogResult>> =>
    api.get<PaginatedResponse<AuditLogResult>>(
      `/activity/entity/${query.entityType}/${query.entityId}`,
      { params: { page: query.page, pageSize: query.pageSize } },
    ),

  getSecuritySummary: (): Promise<SecuritySummary> =>
    api.get<SecuritySummary>("/activity/security"),

  getUserActivity: (query: AuditLogQuery): Promise<PaginatedResponse<AuditLogResult>> =>
    api.get<PaginatedResponse<AuditLogResult>>("/activity/user", {
      params: {
        after: query.after,
        before: query.before,
        entityId: query.entityId,
        entityType: query.entityType,
        ipAddress: query.ipAddress,
        operationType: query.operationType,
        page: query.page,
        pageSize: query.pageSize,
        sortBy: query.sortBy,
        sortDirection: query.sortDirection,
        source: query.source,
      },
    }),

  planActivityExport: (filters: AuditFilterRequest): Promise<AuditExportPlan> =>
    api.get<AuditExportPlan>("/activity/export/plan", { params: filters }),
};
