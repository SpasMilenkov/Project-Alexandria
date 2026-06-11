import { onMounted } from "vue";

import { streamingApi } from "@/api/streaming";
import { usePlayerStore } from "@/stores/stream-player";

/**
 * Unified page size used by both the library grid (display) and the player
 * store (navigation window). Must be the same value in both places so that
 * cursorPage / cursorOffset coordinates are valid after a restore.
 */
export const LIBRARY_PAGE_SIZE = 50;

/**
 * Restores the player store's lazy-fetch context after a navigation or
 * hard reload destroys the MediaLibraryGrid that originally called setSource().
 *
 * Lives in the layout so it survives all route changes. Always restores
 * against the raw unfiltered library — search context is intentionally not
 * persisted because search results are enqueued as concrete files, not used
 * as a navigation source.
 *
 * Call once in the layout, guarded by streamingEnabled:
 *
 *   if (streamingEnabled) useStreamingMediaContext();
 */
export const useStreamingMediaContext = () => {
  const player = usePlayerStore();

  onMounted(async () => {
    if (!player.sourceId) return;

    // sourceId convention: "library-video" | "library-audio"
    const isVideo = player.sourceId.startsWith("library-video");

    await player.restoreContext(
      (page) =>
        streamingApi.getFilesForStreaming({
          page,
          pageSize: LIBRARY_PAGE_SIZE,
          isVideo,
          query: null, // always raw library — search results live in the queue
        }),
      LIBRARY_PAGE_SIZE,
    );
  });
};
