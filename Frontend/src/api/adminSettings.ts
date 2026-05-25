import {apiClient} from "./client";

export interface UploadPolicy {
  skipClientValidationForTrustedUploads: boolean;
  skipUploadOnHashMatch: boolean;
}

export const adminSettingsApi = {
  getUploadPolicy: async (): Promise<UploadPolicy> => {
    const result = await apiClient.get<UploadPolicy>("/admin/settings/upload-policy");
    return result.data;
  },

  resetUploadPolicy: async (): Promise<UploadPolicy> => {
    const result = await apiClient.delete<UploadPolicy>("/admin/settings/upload-policy");
    return result.data;
  },

  updateUploadPolicy: async (payload: UploadPolicy): Promise<UploadPolicy> => {
    const result = await apiClient.put<UploadPolicy>("/admin/settings/upload-policy", payload);
    return result.data;
  },
};
