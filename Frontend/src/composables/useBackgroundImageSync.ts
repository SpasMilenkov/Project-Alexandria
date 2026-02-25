import { settingsApi } from "@/api/settings";
import { useSettingsStore } from "@/stores/settings";

type SWMessage = Record<string, unknown>;

// Send a message to the SW and await the response via MessageChannel
function swMessage(sw: ServiceWorker, data: SWMessage): Promise<SWMessage> {
  return new Promise((resolve, reject) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = (e) => resolve(e.data);
    channel.port1.onmessageerror = () => reject(new Error("SW message error"));
    sw.postMessage(data, [channel.port2]);
  });
}

async function getActiveSW(): Promise<ServiceWorker | null> {
  if (!("serviceWorker" in navigator)) return null;
  const reg = await navigator.serviceWorker.getRegistration("/");
  return reg?.active ?? null;
}

export function useBackgroundImageSync() {
  const store = useSettingsStore();

  // Called by useSettingsSync after server data arrives
  async function syncBackgroundImage(
    key: string | null,
    updatedAt: string | null,
  ): Promise<void> {
    // No image configured — ensure runtime ref is clear
    if (!key || !updatedAt) {
      store.backgroundImage = null;
      return;
    }

    const sw = await getActiveSW();
    if (!sw) {
      // No SW available — fall back to direct presigned URL (no caching)
      store.backgroundImage = await settingsApi.getBackgroundImageUrl();
      return;
    }

    // Ask SW if this timestamp is already cached
    const { cached } = await swMessage(sw, {
      type: "CHECK_CACHED",
      timestamp: updatedAt,
    });

    if (!cached) {
      // Cache miss — fetch a presigned GET URL and seed the SW
      const presignedUrl = await settingsApi.getBackgroundImageUrl();
      const result = await swMessage(sw, {
        type: "FETCH_AND_CACHE",
        presignedUrl,
        timestamp: updatedAt,
      });

      if (!(result as any).success) {
        console.error("SW failed to cache background image:", result);
        return;
      }
    }

    // SW now has it — point the store at the intercept URL
    // useTheme picks this up reactively and sets it as CSS background
    store.backgroundImage = `/sw/background-image?t=${encodeURIComponent(updatedAt)}`;
  }

  // Full upload flow: request URL → upload to S3 → confirm → sync
  async function uploadBackgroundImage(file: File): Promise<void> {
    const { uploadUrl, objectKey } =
      await settingsApi.requestBackgroundImageUpload();
    await settingsApi.uploadToS3(uploadUrl, file);
    const saved = await settingsApi.confirmBackgroundImageUpload(objectKey);
    store.syncFromServer(saved, store.getSettings);
    await syncBackgroundImage(
      saved.backgroundImageKey,
      saved.backgroundImageUpdatedAt,
    );
  }

  async function deleteBackgroundImage(): Promise<void> {
    await settingsApi.deleteBackgroundImage();

    const sw = await getActiveSW();
    if (sw) await swMessage(sw, { type: "EVICT" });

    store.clearBackgroundImage();
  }

  return { syncBackgroundImage, uploadBackgroundImage, deleteBackgroundImage };
}
