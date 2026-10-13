import { useResizeObserver } from "@vueuse/core";
import { type Ref, computed, ref } from "vue";

export const useHomeWidgetRows = (element: Ref<HTMLElement | null>, rowHeight: () => number) => {
  const height = ref(0);

  useResizeObserver(element, (entries) => {
    const bounds = entries[0]?.contentRect;
    if (!bounds) return;

    height.value = bounds.height;
  });

  return computed(() => Math.max(0, Math.floor(height.value / rowHeight())));
};
