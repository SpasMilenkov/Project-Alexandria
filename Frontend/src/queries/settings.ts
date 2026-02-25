import { settingsApi } from "@/api/settings";
import { defineQueryOptions } from "@pinia/colada";

export const SETTINGS_QUERY_KEYS = {
  root: ["settings"] as const,
  appearance: () => [...SETTINGS_QUERY_KEYS.root, "appearance"],
  behavior: () => [...SETTINGS_QUERY_KEYS.root, "behavior"],
};

export const appearanceSettings = defineQueryOptions(() => ({
  key: SETTINGS_QUERY_KEYS.appearance(),
  query: () => settingsApi.getAppearance(),
  staleTime: 60000, // settings don't change frequently
}));

export const behaviorSettings = defineQueryOptions(() => ({
  key: SETTINGS_QUERY_KEYS.behavior(),
  query: () => settingsApi.getBehavior(),
  staleTime: 60000,
}));
