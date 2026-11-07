import { api } from "./client";

export interface UploadPolicy {
  skipClientValidationForTrustedUploads: boolean;
  skipUploadOnHashMatch: boolean;
}

export const adminSettingsApi = {
  getUploadPolicy: (): Promise<UploadPolicy> =>
    api.get<UploadPolicy>("/admin/settings/upload-policy"),

  resetUploadPolicy: (): Promise<UploadPolicy> =>
    api.delete<UploadPolicy>("/admin/settings/upload-policy"),

  updateUploadPolicy: (payload: UploadPolicy): Promise<UploadPolicy> =>
    api.put<UploadPolicy>("/admin/settings/upload-policy", payload),
};
