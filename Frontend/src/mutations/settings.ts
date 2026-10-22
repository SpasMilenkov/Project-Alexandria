import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { type AppearanceSettings, type BehaviorSettings, settingsApi } from "@/api/settings";
import { SETTINGS_QUERY_KEYS } from "@/queries/settings";
import { useAuthStore } from "@/stores/auth";
import { useSettingsStore } from "@/stores/settings";

interface OwnerContext { ownerId: string | null }

const useOwnerGuard = () => {
  const authStore = useAuthStore();
  const currentId = () => authStore.user?.user.id ?? null;

  return {
    capture: (): OwnerContext => ({ ownerId: currentId() }),
    isSameOwner: (ctx?: Partial<OwnerContext>): boolean =>
      Boolean(ctx?.ownerId) && currentId() === ctx?.ownerId,
  };
}

export const updateAppearance = defineMutation(() => {
  const queryCache = useQueryCache();
  const settingsStore = useSettingsStore();
  const { capture, isSameOwner } = useOwnerGuard();

  return useMutation({
    mutation: (data: AppearanceSettings): Promise<AppearanceSettings> =>
      settingsApi.updateAppearance(data),
    onMutate: capture,
    onSuccess(data: AppearanceSettings, _vars: AppearanceSettings, ctx: OwnerContext): void {
      if (!isSameOwner(ctx)) return;

      queryCache.setQueryData(SETTINGS_QUERY_KEYS.appearance(), data);

      // Only take what the server owns. The rest of the store already holds
      // the user's latest edits, and a full sync from a slower response
      // would roll them back.
      settingsStore.backgroundImageKey = data.backgroundImageKey;
      settingsStore.backgroundImageUpdatedAt = data.backgroundImageUpdatedAt;
    },
    onError(_err: Error, _vars: AppearanceSettings, ctx: Partial<OwnerContext>): void {
      if (isSameOwner(ctx)) {
        queryCache.invalidateQueries({ key: SETTINGS_QUERY_KEYS.appearance() });
      }
    },
  });
});

export const updateBehavior = defineMutation(() => {
  const queryCache = useQueryCache();
  const { capture, isSameOwner } = useOwnerGuard();

  return useMutation({
    mutation: (data: BehaviorSettings): Promise<BehaviorSettings> =>
      settingsApi.updateBehavior(data),
    onMutate: capture,
    onSuccess(data: BehaviorSettings, _vars: BehaviorSettings, ctx: OwnerContext): void {
      if (isSameOwner(ctx)) {
        queryCache.setQueryData(SETTINGS_QUERY_KEYS.behavior(), data);
      }
    },
    onError(_err: Error, _vars: BehaviorSettings, ctx: Partial<OwnerContext>): void {
      if (isSameOwner(ctx)) {
        queryCache.invalidateQueries({ key: SETTINGS_QUERY_KEYS.behavior() });
      }
    },
  });
});
