import apiClient from "./client";
import type { FileSummary } from "./directory";

export interface StorageInfo {
  dataAvailableBytes: number;
  dataTotalBytes: number;
  metadataAvailableBytes: number;
  metadataTotalBytes: number;
  dataAvailableGB: number;
  dataTotalGB: number;
  dataUsagePercentage: number;
}

export interface StorageBreakdown {
  sizeByType: Record<string, number>;
  trashSize: number;
  oldFiles: FileSummary[];
}

export const statusApi = {
  getStorageMetrics: async (): Promise<StorageInfo> => {
    const result = await apiClient.get<StorageInfo>("/storage/available");
    return result.data;
  },

  getMyStorage: async (): Promise<StorageBreakdown> => {
    const result = await apiClient.get<StorageBreakdown>("/storage/my-storage");
    return result.data;
  },
};
