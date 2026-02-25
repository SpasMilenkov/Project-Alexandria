// mutations/settings.ts
import { settingsApi } from "@/api/settings";
import { type AppearanceSettings, type BehaviorSettings } from "@/api/settings";
import { SETTINGS_QUERY_KEYS } from "@/queries/settings";
import { useSettingsStore } from "@/stores/settings";
import { defineMutation, useQueryCache } from "@pinia/colada";

export const updateAppearance = defineMutation({
  mutation: (data: AppearanceSettings) => settingsApi.updateAppearance(data),
  onSettled(data) {
    const queryCache = useQueryCache();
    const settingsStore = useSettingsStore();

    queryCache.invalidateQueries({ key: SETTINGS_QUERY_KEYS.appearance() });

    if (data) settingsStore.syncFromServer(data, settingsStore.getSettings);
  },
});

export const updateBehavior = defineMutation({
  mutation: (data: BehaviorSettings) => settingsApi.updateBehavior(data),
  onSettled(data) {
    const queryCache = useQueryCache();
    const settingsStore = useSettingsStore();

    queryCache.invalidateQueries({ key: SETTINGS_QUERY_KEYS.behavior() });

    if (data) settingsStore.syncFromServer(settingsStore.getSettings, data);
  },
});
