import { statusApi } from "@/api/status";
import { defineQueryOptions } from "@pinia/colada";

export const STATUS_QUERY_KEYS = {
  root: ["status"] as const,
  availableStorage: () => [...STATUS_QUERY_KEYS.root, "storage-info"],
  myStorage: () => [...STATUS_QUERY_KEYS.root, "my-storage"],
  serverStatus: () => [...STATUS_QUERY_KEYS.root, "server-status"],
  serverResourceUsage: () => [...STATUS_QUERY_KEYS.root, "server-resources"],
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
