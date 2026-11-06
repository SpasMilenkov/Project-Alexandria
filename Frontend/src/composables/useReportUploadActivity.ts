import { useDocumentVisibility } from "@vueuse/core";
import { onScopeDispose, watch } from "vue";

import type { UploadActivity, UploadOutcome } from "@/features/site-icon/state";

import { useUploadActivityStore } from "@/stores/upload-activity";

/**
 * Publishes one upload window's progress to the shared upload-activity store.
 * `snapshot` returns null while idle; `outcome` is read once when activity ends.
 */
export const useReportUploadActivity = (
  snapshot: () => UploadActivity | null,
  outcome: () => UploadOutcome | null,
) => {
  const store = useUploadActivityStore();
  const visibility = useDocumentVisibility();
  const id = crypto.randomUUID();

  watch(
    snapshot,
    (next, previous) => {
      if (next) {
        store.report(id, next);
        return;
      }

      if (previous) store.settle(id, outcome(), visibility.value === "hidden");
    },
    { immediate: true },
  );

  // An unmounted window can no longer report, so drop its entry instead of leaving stale progress.
  onScopeDispose(() => store.settle(id, null, false));
};
