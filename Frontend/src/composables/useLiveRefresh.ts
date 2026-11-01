import { useDocumentVisibility, useIntervalFn } from "@vueuse/core";
import { type MaybeRefOrGetter, watch } from "vue";

// Colada has no interval refetch, so live panels poll explicitly. Polling pauses while
// the tab is hidden; refetch-on-focus covers catching up when it becomes visible again.
export const useLiveRefresh = (refresh: () => unknown, intervalMs: MaybeRefOrGetter<number>) => {
  const visibility = useDocumentVisibility();

  const { pause, resume } = useIntervalFn(() => {
    refresh();
  }, intervalMs);

  watch(
    visibility,
    (state) => {
      if (state === "hidden") {
        pause();
        return;
      }

      resume();
    },
    { immediate: true },
  );

  return { pause, resume };
};
