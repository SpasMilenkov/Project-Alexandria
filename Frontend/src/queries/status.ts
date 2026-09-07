import { defineQueryOptions } from "@pinia/colada";

import type { MyPreviewsParams } from "@/api/status";

import { statusApi } from "@/api/status";

export interface MyPreviewsFilters {
  fileId?: string;
  createdBefore?: string;
  page?: number;
  pageSize?: number;
}

export const STATUS_QUERY_KEYS = {
  adminStorageOverview: () => [...STATUS_QUERY_KEYS.root, "admin-storage-overview"],
  availableStorage: () => [...STATUS_QUERY_KEYS.root, "storage-info"],
  myPreviews: (filters: MyPreviewsFilters) => [...STATUS_QUERY_KEYS.root, "my-previews", filters],
  myStorage: () => [...STATUS_QUERY_KEYS.root, "my-storage"],
  root: ["status"] as const,
  serverResourceUsage: () => [...STATUS_QUERY_KEYS.root, "server-resources"],
  serverStatus: () => [...STATUS_QUERY_KEYS.root, "server-status"],
  storageSplit: () => [...STATUS_QUERY_KEYS.root, "storage-split"],
  userStorageRanking: (top: number) => [...STATUS_QUERY_KEYS.root, "user-storage-ranking", top],
};

export const serverStatus = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.serverStatus(),
  query: () => statusApi.getServerStatus(),
  staleTime: 30000,
}));

export const serverResourceUsage = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.serverResourceUsage(),
  query: () => statusApi.getServerResources(),
  staleTime: 30000,
}));

export const storageInfo = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.availableStorage(),
  query: () => statusApi.getStorageMetrics(),
  staleTime: 30000,
}));

export const myStorage = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.myStorage(),
  query: () => statusApi.getMyStorage(),
  staleTime: 30000,
}));

export const myPreviews = defineQueryOptions((filters: MyPreviewsFilters = {}) => ({
  key: STATUS_QUERY_KEYS.myPreviews(filters),
  query: () => statusApi.getMyPreviews(filters as MyPreviewsParams),
  staleTime: 30000,
}));

export const adminStorageOverview = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.adminStorageOverview(),
  query: () => statusApi.getAdminStorageOverview(),
  staleTime: 30000,
}));

export const storageSplit = defineQueryOptions(() => ({
  key: STATUS_QUERY_KEYS.storageSplit(),
  query: () => statusApi.getStorageSplit(),
  staleTime: 30000,
}));

export const userStorageRanking = defineQueryOptions((top: number = 10) => ({
  key: STATUS_QUERY_KEYS.userStorageRanking(top),
  query: () => statusApi.getUserStorageRanking(top),
  staleTime: 30000,
}));
