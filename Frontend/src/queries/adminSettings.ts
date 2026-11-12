import { defineQueryOptions } from "@pinia/colada";

import { adminSettingsApi } from "@/api/adminSettings";

export const ADMIN_SETTINGS_QUERY_KEYS = {
  auditRetention: () => [...ADMIN_SETTINGS_QUERY_KEYS.root, "audit-retention"],
  root: ["admin-settings"] as const,
};

export const auditRetention = defineQueryOptions(() => ({
  key: ADMIN_SETTINGS_QUERY_KEYS.auditRetention(),
  query: () => adminSettingsApi.getAuditRetention(),
  staleTime: 60000,
}));
