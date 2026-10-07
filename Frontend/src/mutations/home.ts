import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type { HomeSettings } from "@/types/home";

import { settingsApi } from "@/api/settings";
import { HOME_QUERY_KEYS } from "@/queries/home";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";

export const updateHomeSettings = defineMutation(() => {
  const cache = useQueryCache();
  const auth = useAuthStore();
  const store = useHomeStore();

  return useMutation({
    mutation: async (request: { ownerId: string; epoch: number; value: HomeSettings }) => {
      if (auth.user?.user.id !== request.ownerId || store.ownerEpoch !== request.epoch)
        throw new Error("Your account changed. Reopen Home to continue.");

      return {
        ownerId: request.ownerId,
        epoch: request.epoch,
        value: await settingsApi.updateHome(request.value),
      };
    },
    onSuccess: (response) => {
      if (auth.user?.user.id !== response.ownerId || store.ownerEpoch !== response.epoch) return;

      cache.setQueryData(HOME_QUERY_KEYS.session(response.ownerId, response.epoch), response);
      store.commit(response.ownerId, response.value);
    },
  });
});
