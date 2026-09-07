import type { PreviewKind } from "@/enums/PreviewKind";

import type { FileSummary, PaginatedResponse } from "./directory";

import { apiClient } from "./client";

export interface StorageInfo {
  dataAvailableBytes: number;
  dataTotalBytes: number;
  metadataAvailableBytes: number;
  metadataTotalBytes: number;
  dataAvailableGB: number;
  dataTotalGB: number;
  dataUsagePercentage: number;
  garageCapacityBytes: number;
  garageNodes: GarageNodeCapacity[];
}

export interface StorageBreakdown {
  sizeByType: Record<string, number>;
  trashSize: number;
  oldFiles: FileSummary[];
  filesSize: number;
  previewsSize: number;
  previewsCount: number;
  transcodedSize: number;
  representationsCount: number;
  quotaBytes: number;
  usedBytes: number;
}

export interface UserPreview {
  previewId: string;
  fileId: string;
  fileName: string;
  kind: PreviewKind;
  sizeBytes: number;
  createdAt: string; // ISO 8601
}

export interface MyPreviewsParams {
  fileId?: string;
  createdBefore?: string; // ISO 8601
  page?: number;
  pageSize?: number;
}

export interface GarageNodeCapacity {
  nodeId: string;
  roleCapacityBytes: number;
  connected: boolean;
}

export interface AdminStorageOverview {
  capacity: StorageInfo;
  totalAssignedQuotaBytes: number;
  totalUsers: number;
}

export interface DeletePreviewResult {
  freedBytes: number;
}

export interface DeletePreviewsByFileResult {
  deletedCount: number;
  freedBytes: number;
}

export interface StorageSplit {
  filesSize: number;
  previewsSize: number;
  transcodedSize: number;
  trashSize: number;
}

export interface UserStorageRank {
  userId: string;
  userName: string;
  filesSize: number;
  previewsSize: number;
  transcodedSize: number;
  usedBytes: number;
  quotaBytes: number;
}

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface HealthCheckEntry {
  name: string;
  status: HealthStatus;
  description: string | null;
  duration: number; // Ms
  tags: string[];
  error: string | null;
  data: Record<string, unknown>;
}

export interface HealthSummary {
  healthy: number;
  degraded: number;
  unhealthy: number;
}

export interface ServerStatusResponse {
  status: HealthStatus;
  checkedAt: string; // ISO 8601
  duration: number; // Ms
  summary: HealthSummary;
  checks: HealthCheckEntry[];
}

export interface ProcessInfo {
  cpuTimeSeconds: number;
  workingSetMb: number;
  gcTotalMemoryMb: number;
  memoryLimitMb: number;
  memoryUsagePercent: number;
  threadCount: number;
  uptime: string; // "hh:mm:ss"
  gen0Collections: number;
  gen1Collections: number;
  gen2Collections: number;
}

export interface ServerResourcesResponse {
  process: ProcessInfo;
  checkedAt: string;
}

export const statusApi = {
  getMyStorage: async (): Promise<StorageBreakdown> => {
    const result = await apiClient.get<StorageBreakdown>("/storage/my-storage");
    return result.data;
  },

  getMyPreviews: async (params?: MyPreviewsParams): Promise<PaginatedResponse<UserPreview>> => {
    const result = await apiClient.get<PaginatedResponse<UserPreview>>("/storage/my-previews", {
      params,
    });
    return result.data;
  },

  deletePreview: async (previewId: string): Promise<DeletePreviewResult> => {
    const result = await apiClient.delete<DeletePreviewResult>(`/storage/previews/${previewId}`);
    return result.data;
  },

  deletePreviewsByFile: async (
    fileId: string,
    createdBefore?: string,
  ): Promise<DeletePreviewsByFileResult> => {
    const result = await apiClient.delete<DeletePreviewsByFileResult>(
      `/storage/previews/by-file/${fileId}`,
      { params: createdBefore ? { createdBefore } : undefined },
    );
    return result.data;
  },

  getAdminStorageOverview: async (): Promise<AdminStorageOverview> => {
    const result = await apiClient.get<AdminStorageOverview>("/admin/storage/overview");
    return result.data;
  },

  getStorageSplit: async (): Promise<StorageSplit> => {
    const result = await apiClient.get<StorageSplit>("/admin/storage/split");
    return result.data;
  },

  getUserStorageRanking: async (top = 10): Promise<UserStorageRank[]> => {
    const result = await apiClient.get<UserStorageRank[]>("/admin/storage/users", {
      params: { top },
    });
    return result.data;
  },

  getServerResources: async (): Promise<ServerResourcesResponse> => {
    const result = await apiClient.get<ServerResourcesResponse>("/monitoring/resources");
    return result.data;
  },

  getServerStatus: async (): Promise<ServerStatusResponse> => {
    const result = await apiClient.get<ServerStatusResponse>("/monitoring");
    return result.data;
  },

  getStorageMetrics: async (): Promise<StorageInfo> => {
    const result = await apiClient.get<StorageInfo>("/storage/available");
    return result.data;
  },
};
