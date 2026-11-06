import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { UploadActivity, UploadOutcome } from "@/features/site-icon/state";

/**
 * Combined progress of every open upload window, so app-wide chrome (the tab icon and
 * title) can show uploads without reaching into modal-local state.
 */
export const useUploadActivityStore = defineStore("upload-activity", () => {
  const sources = ref(new Map<string, UploadActivity>());
  const outcome = ref<UploadOutcome | null>(null);

  const current = computed((): UploadActivity | null => {
    const list = [...sources.value.values()];

    if (list.length === 0) return null;

    // Weight by file count so one small upload doesn't drag a big batch's percentage around.
    const weights = list.map((activity) => Math.max(activity.fileCount, 1));
    const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
    const progress =
      list.reduce((sum, activity, index) => sum + activity.progress * weights[index], 0) /
      totalWeight;

    const fileCount = list.reduce((sum, activity) => sum + activity.fileCount, 0);
    const kind = list.some((activity) => activity.kind === "extract") ? "extract" : "upload";
    const label = list.length === 1 ? list[0].label : null;

    return { fileCount, kind, label, progress };
  });

  const report = (id: string, activity: UploadActivity) => {
    sources.value.set(id, activity);
  };

  // Outcomes only matter when nobody was watching; a visible upload window already says it.
  const settle = (id: string, result: UploadOutcome | null, inBackground: boolean) => {
    sources.value.delete(id);

    if (!result || !inBackground) return;

    if (outcome.value === "failed") return;

    outcome.value = result;
  };

  const clearOutcome = () => {
    outcome.value = null;
  };

  return { clearOutcome, current, outcome, report, settle };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useUploadActivityStore, import.meta.hot));
}
