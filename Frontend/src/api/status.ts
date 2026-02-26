import type { FileSummary } from "./directory";

import apiClient from "./client";

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
