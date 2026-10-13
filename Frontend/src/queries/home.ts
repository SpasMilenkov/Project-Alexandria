import { defineQueryOptions } from "@pinia/colada";

import { settingsApi } from "@/api/settings";

export const HOME_QUERY_KEYS = {
  root: ["settings", "home"] as const,
  settings: (userId: string) => ["settings", "home", userId] as const,
  session: (userId: string, epoch: number) => ["settings", "home", userId, epoch] as const,
};

export const homeSettings = defineQueryOptions((params: { userId: string; epoch: number }) => ({
  key: HOME_QUERY_KEYS.session(params.userId, params.epoch),
  enabled: Boolean(params.userId),
  staleTime: 60_000,
  query: async () => ({
    ownerId: params.userId,
    epoch: params.epoch,
    value: await settingsApi.getHome(),
  }),
}));

export const homeTimeZones = defineQueryOptions((params: { enabled: boolean }) => ({
  key: ["settings", "home-time-zones"],
  enabled: params.enabled,
  staleTime: 3_600_000,
  query: settingsApi.getHomeTimeZones,
}));
