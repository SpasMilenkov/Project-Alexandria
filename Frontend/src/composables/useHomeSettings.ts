import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, watch } from "vue";

import { updateHomeSettings } from "@/mutations/home";
import { HOME_QUERY_KEYS, homeSettings, homeTimeZones } from "@/queries/home";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { cloneHomeSettings } from "@/utils/home-layout";

export const useHomeSettings = () => {
  const auth = useAuthStore();
  const store = useHomeStore();
  const cache = useQueryCache();
  const userId = computed(() => auth.user?.user.id ?? "");

  watch(
    userId,
    (nextOwner, previousOwner) => {
      store.setOwner(nextOwner || null);

      if (previousOwner) {
        cache.cancelQueries({ key: HOME_QUERY_KEYS.settings(previousOwner) });
        for (const entry of cache.getEntries({ key: HOME_QUERY_KEYS.settings(previousOwner) })) {
          cache.remove(entry);
        }
      }
    },
    { immediate: true, flush: "sync" },
  );

  const query = useQuery(() => homeSettings({ userId: userId.value, epoch: store.ownerEpoch }));

  useQuery(() => homeTimeZones({ enabled: Boolean(userId.value) }));

  const mutation = updateHomeSettings();
  const isSaving = computed(
    () => mutation.isLoading.value && mutation.variables.value?.epoch === store.ownerEpoch,
  );

  watch(
    query.data,
    (response) => {
      if (response && response.ownerId === userId.value && response.epoch === store.ownerEpoch)
        store.hydrate(response.ownerId, response.value);
    },
    { immediate: true },
  );

  const save = () => {
    if (!store.draft || !userId.value)
      return Promise.reject(new Error("Open customization before saving."));

    return mutation.mutateAsync({
      ownerId: userId.value,
      epoch: store.ownerEpoch,
      value: cloneHomeSettings(store.draft),
    });
  };

  return {
    error: query.error,
    isLoading: query.isLoading,
    isSaving,
    refresh: query.refresh,
    save,
  };
};
