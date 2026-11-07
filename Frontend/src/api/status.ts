import type { PreviewKind } from "@/enums/PreviewKind";

import type { PaginatedResponse } from "./directory";

import { api } from "./client";

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
  getMyStorage: (): Promise<StorageBreakdown> => api.get<StorageBreakdown>("/storage/my-storage"),

  getMyPreviews: (params?: MyPreviewsParams): Promise<PaginatedResponse<UserPreview>> =>
    api.get<PaginatedResponse<UserPreview>>("/storage/my-previews", {
      params,
    }),

  deletePreview: (previewId: string): Promise<DeletePreviewResult> =>
    api.delete<DeletePreviewResult>(`/storage/previews/${previewId}`),

  deletePreviewsByFile: (
    fileId: string,
    createdBefore?: string,
  ): Promise<DeletePreviewsByFileResult> =>
    api.delete<DeletePreviewsByFileResult>(`/storage/previews/by-file/${fileId}`, {
      params: createdBefore ? { createdBefore } : undefined,
    }),

  getAdminStorageOverview: (): Promise<AdminStorageOverview> =>
    api.get<AdminStorageOverview>("/admin/storage/overview"),

  getStorageSplit: (): Promise<StorageSplit> => api.get<StorageSplit>("/admin/storage/split"),

  getUserStorageRanking: (top = 10): Promise<UserStorageRank[]> =>
    api.get<UserStorageRank[]>("/admin/storage/users", {
      params: { top },
    }),

  getServerResources: (): Promise<ServerResourcesResponse> =>
    api.get<ServerResourcesResponse>("/monitoring/resources"),

  getServerStatus: (): Promise<ServerStatusResponse> =>
    api.get<ServerStatusResponse>("/monitoring"),

  getStorageMetrics: (): Promise<StorageInfo> => api.get<StorageInfo>("/storage/available"),
};
