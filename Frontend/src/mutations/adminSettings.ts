import { defineMutation, useQueryCache } from "@pinia/colada";

import { type UploadPolicy, adminSettingsApi } from "@/api/adminSettings";
import { ADMIN_SETTINGS_QUERY_KEYS } from "@/queries/adminSettings";

export const updateUploadPolicy = defineMutation({
  mutation: (data: UploadPolicy) => adminSettingsApi.updateUploadPolicy(data),
  onSettled() {
    useQueryCache().invalidateQueries({
      key: ADMIN_SETTINGS_QUERY_KEYS.uploadPolicy(),
    });
  },
});

export const resetUploadPolicy = defineMutation({
  mutation: () => adminSettingsApi.resetUploadPolicy(),
  onSettled() {
    useQueryCache().invalidateQueries({
      key: ADMIN_SETTINGS_QUERY_KEYS.uploadPolicy(),
    });
  },
});
