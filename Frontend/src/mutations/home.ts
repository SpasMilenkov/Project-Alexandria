import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { type HomeBoardChanges, homeApi } from "@/api/home";
import { HOME_QUERY_KEYS } from "@/queries/home";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";

export const updateHomeSettings = defineMutation(() => {
  const cache = useQueryCache();
  const auth = useAuthStore();
  const store = useHomeStore();

  return useMutation({
    mutation: async (request: { ownerId: string; epoch: number; changes: HomeBoardChanges }) => {
      if (auth.user?.user.id !== request.ownerId || store.ownerEpoch !== request.epoch)
        throw new Error("Your account changed. Reopen Home to continue.");

      return {
        ownerId: request.ownerId,
        epoch: request.epoch,
        value: await homeApi.saveChanges(request.changes),
      };
    },
    onSuccess: (response) => {
      if (auth.user?.user.id !== response.ownerId || store.ownerEpoch !== response.epoch) return;

      cache.setQueryData(HOME_QUERY_KEYS.session(response.ownerId, response.epoch), response);
      store.commit(response.ownerId, response.value);
    },
  });
});

export const resetHomeSettings = defineMutation(() => {
  const cache = useQueryCache();
  const auth = useAuthStore();
  const store = useHomeStore();

  return useMutation({
    mutation: async (request: { ownerId: string; epoch: number }) => {
      if (auth.user?.user.id !== request.ownerId || store.ownerEpoch !== request.epoch)
        throw new Error("Your account changed. Reopen Home to continue.");

      cache.cancelQueries({ key: HOME_QUERY_KEYS.session(request.ownerId, request.epoch) });

      return {
        ownerId: request.ownerId,
        epoch: request.epoch,
        value: await homeApi.resetBoard(),
      };
    },
    onSuccess: (response) => {
      if (auth.user?.user.id !== response.ownerId || store.ownerEpoch !== response.epoch) return;

      const key = HOME_QUERY_KEYS.session(response.ownerId, response.epoch);
      cache.cancelQueries({ key });
      cache.setQueryData(key, response);
      store.commit(response.ownerId, response.value);
    },
  });
});
