import { onMounted } from "vue";

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

  onMounted(async () => {
    const ownerId = auth.user?.user.id ?? null;
    await player.restorePlaybackContext(ownerId);
  });
};
