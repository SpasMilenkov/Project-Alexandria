import { watch } from "vue";

import { useAuthStore } from "@/stores/auth";
import { usePlayerStore } from "@/stores/stream-player";

/**
 * Restores the player store after navigation or a hard reload destroys the
 * component that originally started playback.
 *
 * Lives in the layout so it survives all route changes. The store owns the
 * source descriptor and shuffle session handle; this composable only waits
 * for an authenticated owner and hands control to the store, which reconnects
 * the shuffle session or re-establishes the sequential window from the saved
 * anchor. Search context is intentionally not persisted: search results live
 * in the manual queue, not in a navigation source.
 *
 * Call once in the layout, guarded by streamingEnabled:
 *
 *   if (streamingEnabled) useStreamingMediaContext();
 */
export const useStreamingMediaContext = () => {
  const player = usePlayerStore();
  const auth = useAuthStore();

  // Auth can resolve after mount; restore is idempotent per owner (R5), so
  // re-running when the owner id settles is safe and required. Without this,
  // a persisted context never gets its order rebuilt and context clicks fail.
  watch(
    () => auth.user?.user.id ?? null,
    (ownerId) => {
      void player.restore(ownerId);
    },
    { immediate: true },
  );
};
