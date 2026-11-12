import type { AuditExportFormat } from "@/enums/audit-export-format";
import type { AuditSortBy } from "@/enums/audit-sort-by";
import type { LoginFailureReason } from "@/enums/login-failure-reason";
import type { SortDirection } from "@/enums/SortDirection";

import type {
  AuditExportBounds,
  AuditExportPlan,
  AuditFilterRequest,
  AuditLogResult,
} from "./activity";
import type { PaginatedResponse } from "./directory";

import { api } from "./client";

export interface AdminAuditFilterRequest extends AuditFilterRequest {
  userId?: string;
  ownerId?: string;
}

export interface AdminAuditQuery extends AdminAuditFilterRequest {
  page: number;
  pageSize: number;
  sortBy?: AuditSortBy;
  sortDirection?: SortDirection;
}

export interface AccountAccessSummary {
  userId: string;
  name: string;
  email: string;
  // Latest sign-in ever recorded, so quiet accounts still show when they were last used.
  lastSignInAt: string | null;
  signIns: number;
  newNetworks: number;
  failedSignIns: number;
  liveSessions: number;
  sessionUserAgents: string[];
  lockoutEnd: string | null;
}

export interface FailedSignInReasonCount {
  reason: LoginFailureReason;
  count: number;
}

export interface FailedSignInSource {
  ipAddress: string;
  count: number;
  accountCount: number;
  lastSeen: string;
}

export interface UnknownAccountAttempt {
  email: string;
  count: number;
  lastSeen: string;
}

export interface AccessNetwork {
  ipAddress: string;
  accountCount: number;
  signIns: number;
  // First sign-in from this address ever recorded, not just inside the window.
  firstSeen: string;
  lastSeen: string;
}

export interface AccessOverview {
  windowDays: number;
  activeAccounts: number;
  totalAccounts: number;
  liveSessions: number;
  lockedAccounts: number;
  failedSignIns: number;
  failedSignInsPrevious: number;
  accounts: AccountAccessSummary[];
  failedByReason: FailedSignInReasonCount[];
  failedSources: FailedSignInSource[];
  unknownAccountAttempts: UnknownAccountAttempt[];
  networks: AccessNetwork[];
}

export const adminAuditApi = {
  exportAudit: (
    filters: AdminAuditFilterRequest,
    format: AuditExportFormat,
    bounds?: AuditExportBounds,
  ): Promise<Blob> =>
    api.get<Blob>("/admin/audit/export", {
      params: { ...filters, ...bounds, format },
      responseType: "blob",
      timeout: 300_000,
    }),

  getAccessOverview: (days: number): Promise<AccessOverview> =>
    api.get<AccessOverview>("/admin/audit/overview", { params: { days } }),

  planAuditExport: (filters: AdminAuditFilterRequest): Promise<AuditExportPlan> =>
    api.get<AuditExportPlan>("/admin/audit/export/plan", { params: filters }),

  searchAudit: (query: AdminAuditQuery): Promise<PaginatedResponse<AuditLogResult>> =>
    api.get<PaginatedResponse<AuditLogResult>>("/admin/audit", { params: query }),
};
