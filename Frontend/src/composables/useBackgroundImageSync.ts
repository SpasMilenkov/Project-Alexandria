import { useQueryCache } from "@pinia/colada";

import { settingsApi } from "@/api/settings";
import { SETTINGS_QUERY_KEYS } from "@/queries/settings";
import { useAuthStore } from "@/stores/auth";
import { useSettingsStore } from "@/stores/settings";
import {
  type BackgroundImageLoad,
  finishBackgroundImageLoad,
  getBackgroundImageLoadGeneration,
  hasBackgroundImageObjectUrl,
  invalidateBackgroundImageLoads,
  isCurrentBackgroundImageLoad,
  setBackgroundImageObjectUrl,
  startBackgroundImageLoad,
} from "@/utils/backgroundImageObjectUrl";
import { logger } from "@/utils/logger";

interface PendingBackgroundImage {
  generation: number;
  key: string;
  promise: Promise<void>;
  updatedAt: string;
}

let pendingBackgroundImage: PendingBackgroundImage | null = null;

const isAbortError = (err: unknown): boolean =>
  err instanceof DOMException && err.name === "AbortError";

const fetchBackgroundImage = async (url: string, signal: AbortSignal): Promise<Blob> => {
  const response = await fetch(url, {
    cache: "no-store",
    credentials: "same-origin",
    signal,
  });

  if (!response.ok) {
    throw new Error(`Background image fetch failed: ${response.status}`);
  }

  const contentType = response.headers.get("content-type") ?? "";

  if (!contentType.startsWith("image/")) {
    throw new Error("Background image response was not an image");
  }

  return response.blob();
};

export const useBackgroundImageSync = () => {
  const store = useSettingsStore();
  const authStore = useAuthStore();
  const queryCache = useQueryCache();

  const loadBackgroundImage = (
    key: string,
    updatedAt: string,
  ): { load: BackgroundImageLoad; promise: Promise<void> } => {
    const ownerId = authStore.user?.user?.id ?? null;
    const load = startBackgroundImageLoad();

    const promise = (async () => {
      try {
        const imageUrl = await settingsApi.getBackgroundImageUrl();
        const blob = await fetchBackgroundImage(imageUrl, load.controller.signal);
        const ownerChanged = (authStore.user?.user?.id ?? null) !== ownerId;

        if (
          !isCurrentBackgroundImageLoad(load) ||
          ownerChanged ||
          store.backgroundImageKey !== key ||
          store.backgroundImageUpdatedAt !== updatedAt
        ) {
          return;
        }

        const objectUrl = URL.createObjectURL(blob);

        setBackgroundImageObjectUrl(objectUrl, key, updatedAt);
        store.setBackgroundImage(objectUrl);
      } catch (err: unknown) {
        if (isAbortError(err) || !isCurrentBackgroundImageLoad(load)) {
          return;
        }

        logger.error("Unable to load background image:", err);
      } finally {
        finishBackgroundImageLoad(load);
      }
    })();

    return { load, promise };
  };

  const syncBackgroundImage = (key: string | null, updatedAt: string | null): Promise<void> => {
    if (!key || !updatedAt) {
      invalidateBackgroundImageLoads();
      store.clearBackgroundImage();
      return Promise.resolve();
    }

    if (store.backgroundImageKey && store.backgroundImageKey !== key) {
      return Promise.resolve();
    }

    if (store.backgroundImageUpdatedAt && store.backgroundImageUpdatedAt !== updatedAt) {
      return Promise.resolve();
    }

    if (hasBackgroundImageObjectUrl(key, updatedAt)) {
      return Promise.resolve();
    }

    if (pendingBackgroundImage?.key === key && pendingBackgroundImage.updatedAt === updatedAt) {
      if (pendingBackgroundImage.generation === getBackgroundImageLoadGeneration()) {
        return pendingBackgroundImage.promise;
      }

      pendingBackgroundImage = null;
    }

    const { load, promise: loadPromise } = loadBackgroundImage(key, updatedAt);
    const promise = loadPromise.finally(() => {
      if (pendingBackgroundImage?.promise === promise) {
        pendingBackgroundImage = null;
      }
    });

    pendingBackgroundImage = {
      generation: load.generation,
      key,
      promise,
      updatedAt,
    };

    return promise;
  };

  const uploadBackgroundImage = async (file: File): Promise<void> => {
    const saved = await settingsApi.uploadBackgroundImage(file);

    store.syncFromServer(saved, store.getSettings);
    queryCache.invalidateQueries({ key: SETTINGS_QUERY_KEYS.appearance() });

    await syncBackgroundImage(saved.backgroundImageKey, saved.backgroundImageUpdatedAt);
  };

  const deleteBackgroundImage = async (): Promise<void> => {
    invalidateBackgroundImageLoads();

    await settingsApi.deleteBackgroundImage();

    store.clearBackgroundImage();
  };

  return { deleteBackgroundImage, syncBackgroundImage, uploadBackgroundImage };
};
