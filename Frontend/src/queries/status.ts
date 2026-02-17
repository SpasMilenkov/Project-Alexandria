import { statusApi } from "@/api/status";
import { defineQueryOptions } from "@pinia/colada";

export const STATUS_QUERY_KEYS = {
  root: ["status"] as const,
  availableStorage: () => [...STATUS_QUERY_KEYS.root, "storageInfo"],
  myStorage: () => [...STATUS_QUERY_KEYS.root, "myStorage"],
};

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
