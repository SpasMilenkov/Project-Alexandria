import { adminSettingsApi } from "@/api/adminSettings";
import { defineQueryOptions } from "@pinia/colada";

export const ADMIN_SETTINGS_QUERY_KEYS = {
  root: ["admin-settings"] as const,
  uploadPolicy: () => [...ADMIN_SETTINGS_QUERY_KEYS.root, "upload-policy"],
};

export const uploadPolicy = defineQueryOptions(() => ({
  key: ADMIN_SETTINGS_QUERY_KEYS.uploadPolicy(),
  query: () => adminSettingsApi.getUploadPolicy(),
  staleTime: 60000,
}));
