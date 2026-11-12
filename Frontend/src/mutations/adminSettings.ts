import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { adminSettingsApi } from "@/api/adminSettings";
import { ADMIN_SETTINGS_QUERY_KEYS } from "@/queries/adminSettings";

export const updateAuditRetention = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (retentionDays: number) => adminSettingsApi.updateAuditRetention(retentionDays),
    onSuccess(data) {
      queryCache.setQueryData(ADMIN_SETTINGS_QUERY_KEYS.auditRetention(), data);
    },
    onSettled() {
      queryCache.invalidateQueries({ key: ADMIN_SETTINGS_QUERY_KEYS.auditRetention() });
    },
  });
});
