import { defineQueryOptions } from "@pinia/colada";

import { authApi } from "@/api/auth";

export const SESSION_QUERY_KEYS = {
  list: (userId: string) => [...SESSION_QUERY_KEYS.root, userId],
  root: ["auth-sessions"] as const,
};

export const userSessions = defineQueryOptions((userId: string) => ({
  enabled: Boolean(userId),
  key: SESSION_QUERY_KEYS.list(userId),
  query: () => authApi.getSessions(),
  staleTime: 30_000,
}));
