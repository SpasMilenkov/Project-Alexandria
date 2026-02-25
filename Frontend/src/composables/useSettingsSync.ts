import { watch } from "vue";
import { useQuery } from "@pinia/colada";
import { useSettingsStore } from "@/stores/settings";
import { appearanceSettings, behaviorSettings } from "@/queries/settings";
import { updateAppearance, updateBehavior } from "@/mutations/settings";
import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import type { AppearanceSettings, BehaviorSettings } from "@/api/settings";

export function useSettingsSync() {
  const store = useSettingsStore();
  const { syncBackgroundImage } = useBackgroundImageSync();

  const appearanceQuery = useQuery(appearanceSettings());
  const behaviorQuery = useQuery(behaviorSettings());

  const appearanceMutation = updateAppearance();
  const behaviorMutation = updateBehavior();

  watch(
    () => appearanceQuery.data.value,
    async (data) => {
      if (!data) return;
      store.syncFromServer(data, store.getSettings);
      // Trigger SW cache check / seed after store has new key + timestamp
      await syncBackgroundImage(
        data.backgroundImageKey,
        data.backgroundImageUpdatedAt,
      );
    },
    { immediate: true },
  );

  watch(
    () => behaviorQuery.data.value,
    (data) => {
      if (data) store.syncBehaviorFromServer(data);
    },
    { immediate: true },
  );

  const saveAppearance = (values: AppearanceSettings) =>
    appearanceMutation.mutateAsync(values);

  const saveBehavior = (values: BehaviorSettings) =>
    behaviorMutation.mutateAsync(values);

  return {
    isLoadingAppearance: appearanceQuery.isLoading,
    isLoadingBehavior: behaviorQuery.isLoading,
    isSavingAppearance: appearanceMutation.isLoading,
    isSavingBehavior: behaviorMutation.isLoading,
    appearanceError: appearanceQuery.error,
    behaviorError: behaviorQuery.error,
    saveAppearance,
    saveBehavior,
  };
}
