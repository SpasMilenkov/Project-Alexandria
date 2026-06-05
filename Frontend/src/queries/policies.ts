import { defineQueryOptions } from "@pinia/colada";

import { policyApi } from "@/api/policy";

export const POLICY_QUERY_KEYS = {
  root: ["policies"] as const,
  byDirectory: (directoryId: string) => [...POLICY_QUERY_KEYS.root, "by-directory", directoryId],
};

export const getPolicyByDirectory = defineQueryOptions((directoryId: string) => ({
  key: POLICY_QUERY_KEYS.byDirectory(directoryId),
  query: () => policyApi.getByDirectory(directoryId),
  staleTime: 30000,
}));
