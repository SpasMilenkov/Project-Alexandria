import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, watch } from "vue";

import { updateHomeSettings } from "@/mutations/home";
import { HOME_QUERY_KEYS, homeSettings, homeTimeZones } from "@/queries/home";
import { HOME_MEDIA_QUERY_KEYS } from "@/queries/home-media";
import { HOME_STATUS_ACTIVITY_QUERY_KEYS } from "@/queries/home-status-activity";
import { HOME_TOP_SONGS_QUERY_KEYS } from "@/queries/home-top-songs";
import { HOME_WIDGET_QUERY_KEYS } from "@/queries/home-widgets";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { buildHomeChanges } from "@/utils/home-widget-persistence";

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
        const keys = [
          HOME_QUERY_KEYS.settings(previousOwner),
          HOME_WIDGET_QUERY_KEYS.storageOwner(previousOwner),
          HOME_WIDGET_QUERY_KEYS.filesOwner(previousOwner),
          HOME_WIDGET_QUERY_KEYS.foldersOwner(previousOwner),
          HOME_STATUS_ACTIVITY_QUERY_KEYS.statusOwner(previousOwner),
          HOME_STATUS_ACTIVITY_QUERY_KEYS.activityOwner(previousOwner),
          HOME_TOP_SONGS_QUERY_KEYS.owner(previousOwner),
          HOME_MEDIA_QUERY_KEYS.jobsOwner(previousOwner),
          HOME_MEDIA_QUERY_KEYS.recapOwner(previousOwner),
          HOME_MEDIA_QUERY_KEYS.collectionsOwner(previousOwner),
        ];

        for (const key of keys) {
          cache.cancelQueries({ key });

          for (const entry of cache.getEntries({ key })) cache.remove(entry);
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
    if (!store.draft || !store.baseline || !userId.value)
      return Promise.reject(new Error("Open customization before saving."));

    return mutation.mutateAsync({
      ownerId: userId.value,
      epoch: store.ownerEpoch,
      changes: buildHomeChanges(store.baseline, store.draft),
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
