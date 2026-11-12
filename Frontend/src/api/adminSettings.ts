import { api } from "./client";

export interface AuditRetention {
  retentionDays: number;
  minDays: number;
  maxDays: number;
  lastPurgeAt: string | null;
  lastPurgeDeletedCount: number | null;
}

export const adminSettingsApi = {
  getAuditRetention: (): Promise<AuditRetention> =>
    api.get<AuditRetention>("/admin/settings/audit-retention"),

  updateAuditRetention: (retentionDays: number): Promise<AuditRetention> =>
    api.put<AuditRetention>("/admin/settings/audit-retention", { retentionDays }),
};
