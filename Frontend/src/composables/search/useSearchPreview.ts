import { useDebounceFn, useMediaQuery } from "@vueuse/core";
import { type Ref, computed, shallowRef, watch } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import type { SearchPreviewTarget } from "@/types/search-preview-target";

import {
  glassModalContentWide,
  previewModalContent,
  previewModalContentExpanded,
} from "@/utils/modal-ui";

const PREVIEW_DWELL_MS = 120;

const EASE = "ease-[cubic-bezier(0.2,0,0,1)]";

// Desktop preview pane beside the search results. Opening grows the box first
// and settles the content in once it has room; closing fades the content first
// and then shrinks the box. The delays here match the ones on the modal width.
export const useSearchPreview = (
  fileResults: Readonly<Ref<FileResult[]>>,
  directoryResults: Readonly<Ref<DirectorySummaryDto[]>>,
) => {
  // Only a wide screen with a real pointer has room for the pane and a way to
  // hover. Below 80rem the wide modal would have to shift far left to fit,
  // which reads as sliding.
  const canPreview = useMediaQuery("(min-width: 1280px) and (hover: hover) and (pointer: fine)");
  const previewTarget = shallowRef<SearchPreviewTarget | null>(null);

  const isPreviewOpen = computed(() => canPreview.value && previewTarget.value !== null);

  // Keeps the last result in the pane while it collapses, so it fades out in
  // place instead of vanishing before the box has closed.
  const shownPreview = shallowRef<SearchPreviewTarget | null>(null);
  watch(previewTarget, (target) => {
    if (target) {
      shownPreview.value = target;
    }
  });

  const modalContent = computed(() => {
    if (!canPreview.value) {
      return glassModalContentWide;
    }
    return isPreviewOpen.value ? previewModalContentExpanded : previewModalContent;
  });

  // The modal grows by 22rem (2xl to 5xl), exactly the pane plus its gap, so the
  // results column keeps its width while the pane opens beside it.
  const layoutClass = computed(() => {
    if (!canPreview.value) {
      return "";
    }

    const columns = isPreviewOpen.value
      ? "grid-cols-[minmax(0,1fr)_21rem] gap-x-4 delay-0"
      : "grid-cols-[minmax(0,1fr)_0rem] gap-x-0 delay-100";
    return `grid transition-[grid-template-columns,column-gap] duration-200 ${EASE} motion-reduce:transition-none ${columns}`;
  });

  const paneEdgeClass = computed(() =>
    isPreviewOpen.value ? "border-default delay-0" : "border-transparent delay-100",
  );

  const paneContentClass = computed(() =>
    isPreviewOpen.value
      ? "translate-y-0 opacity-100 duration-200 delay-150"
      : "translate-y-1 opacity-0 duration-100 delay-0",
  );

  // A short dwell keeps a quick pass over the list from loading every preview on the way.
  const setPreview = useDebounceFn((target: SearchPreviewTarget) => {
    previewTarget.value = target;
  }, PREVIEW_DWELL_MS);

  const preview = (target: SearchPreviewTarget) => {
    if (canPreview.value) {
      setPreview(target);
    }
  };

  // The preview stays after the pointer leaves, and only clears once its result is gone.
  watch([fileResults, directoryResults], ([files, directories]) => {
    const target = previewTarget.value;
    if (!target) {
      return;
    }

    const stillListed =
      target.kind === "file"
        ? files.some((file) => file.fileId === target.file.fileId)
        : directories.some((dir) => dir.id === target.directory.id);
    if (!stillListed) {
      previewTarget.value = null;
    }
  });

  return {
    canPreview,
    isPreviewOpen,
    layoutClass,
    modalContent,
    paneContentClass,
    paneEdgeClass,
    preview,
    shownPreview,
  };
};
