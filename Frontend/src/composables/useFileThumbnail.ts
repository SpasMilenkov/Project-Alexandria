import { computed, onScopeDispose, ref, watch } from "vue";

import { attemptRefresh } from "@/api/client";
import { fileApi } from "@/api/file";
import { isThumbnailSupportedMimeType } from "@/utils/mimetype.utils";

export interface FileThumbnailSource {
  fileId: string;
  versionId: string;
  mimeType?: string | null;
}

// Retry budget: 5 attempts over ~2min. One uniform loop for every failure,
// no error-type branches: preview-worker generation lag after upload,
// transient nginx 404s (cached 1m server-side), and idle session expiry all
// converge here. Permanent misses (encrypted, unpromoted) burn 6 cheap
// lookups, one initial plus 5 retries, then latch the fallback icon.
const MAX_RETRIES = 5;
const RETRY_DELAYS_MS = [2_000, 5_000, 10_000, 30_000, 60_000];

// Version-scoped thumbnail URL: permanently cacheable. Non-image files 404 and fall through to the icon.
// When mimeType is provided and outside the backend thumbnail allowlist
// (text, archives, unknown), canHaveThumbnail is false so callers can skip
// mounting the <img> entirely instead of firing a doomed request.
// mimeType omitted preserves the legacy behavior (URL always built).
export const useFileThumbnail = (source: () => FileThumbnailSource) => {
  const thumbnailLoaded = ref(false);
  const thumbnailErrored = ref(false);
  const retryCount = ref(0);
  let retryTimer: ReturnType<typeof setTimeout> | null = null;

  const clearRetryTimer = () => {
    if (retryTimer !== null) {
      clearTimeout(retryTimer);
      retryTimer = null;
    }
  };

  onScopeDispose(clearRetryTimer);

  const thumbnailUrl = computed(() => {
    const base = fileApi.getThumbnailUrlForVersion(source().fileId, source().versionId);
    return retryCount.value > 0 ? `${base}?retry=${retryCount.value}` : base;
  });

  const canHaveThumbnail = computed(() => {
    const mimeType = source().mimeType;
    if (mimeType === undefined) return true;
    return isThumbnailSupportedMimeType(mimeType);
  });

  // Reset only on URL-affecting changes (fileId/versionId). mimeType is
  // deliberately excluded from the reset decision: it feeds canHaveThumbnail
  // reactively, but a MIME-only update leaves the src byte-identical, so the
  // browser fires no new load/error event and a reset here would strand
  // thumbnailLoaded=false forever (loaded image hidden behind the fallback
  // icon, zero requests). Note the comparison must live in the callback:
  // merely calling source() in a watch getter would subscribe to mimeType
  // anyway since the object literal reads every field.
  watch(source, (current, previous) => {
    if (!previous) return;
    if (current.fileId === previous.fileId && current.versionId === previous.versionId) return;
    thumbnailLoaded.value = false;
    thumbnailErrored.value = false;
    retryCount.value = 0;
    clearRetryTimer();
  });

  const onThumbnailLoad = () => {
    thumbnailLoaded.value = true;
    clearRetryTimer();
  };

  const onThumbnailError = () => {
    if (thumbnailLoaded.value || thumbnailErrored.value || retryTimer !== null) return;
    if (retryCount.value === 0) {
      // Best-effort session heal for the idle-expiry case: <img> cannot run
      // the axios refresh flow, so kick one off here. Concurrent callers
      // share a single in-flight POST via refreshPromise in api/client, and
      // failures are ignored, the scheduled retries cover it either way.
      attemptRefresh().catch(() => undefined);
    }
    if (retryCount.value >= MAX_RETRIES) {
      thumbnailErrored.value = true;
      return;
    }
    const { fileId, versionId } = source();
    const attempt = retryCount.value;
    retryTimer = setTimeout(() => {
      retryTimer = null;
      const current = source();
      if (current.fileId !== fileId || current.versionId !== versionId || thumbnailLoaded.value) {
        return;
      }
      retryCount.value = attempt + 1;
    }, RETRY_DELAYS_MS[attempt]);
  };

  return {
    canHaveThumbnail,
    thumbnailErrored,
    thumbnailLoaded,
    thumbnailUrl,
    onThumbnailError,
    onThumbnailLoad,
  };
};
