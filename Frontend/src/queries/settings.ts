import { defineQueryOptions } from "@pinia/colada";

import { settingsApi } from "@/api/settings";

export const SETTINGS_QUERY_KEYS = {
  appearance: () => [...SETTINGS_QUERY_KEYS.root, "appearance"],
  behavior: () => [...SETTINGS_QUERY_KEYS.root, "behavior"],
  root: ["settings"] as const,
};

export const appearanceSettings = defineQueryOptions(() => ({
  key: SETTINGS_QUERY_KEYS.appearance(),
  query: () => settingsApi.getAppearance(),
  staleTime: 60000,
}));

export const behaviorSettings = defineQueryOptions(() => ({
  key: SETTINGS_QUERY_KEYS.behavior(),
  query: () => settingsApi.getBehavior(),
  staleTime: 60000,
}));
