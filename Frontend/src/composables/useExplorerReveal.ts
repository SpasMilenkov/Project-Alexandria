import { type Ref, nextTick, watch } from "vue";

import type { FileResult } from "@/api/file";

interface ExplorerRevealOptions {
  fileId: string;
  directoryId: string | null;
  currentDirId: Ref<string | null>;
  filesList: Ref<FileResult[]>;
  hasMoreFiles: () => boolean;
  loadMoreFiles: () => void;
  select: (fileId: string) => void;
}

/**
 * Pages through a folder until it lists the file, then selects it and scrolls it into view.
 * Gives up when the folder runs out of pages, since the file may have moved since the link
 * was made, and when the user leaves the folder first.
 */
export const useExplorerReveal = ({
  fileId,
  directoryId,
  currentDirId,
  filesList,
  hasMoreFiles,
  loadMoreFiles,
  select,
}: ExplorerRevealOptions) => {
  const scrollToFile = async () => {
    await nextTick();

    const element = document.querySelector<HTMLElement>(
      `[data-file-id="${CSS.escape(fileId)}"]`,
    );

    element?.scrollIntoView({ block: "center" });
    element?.focus({ preventScroll: true });
  };

  const check = (files: FileResult[]) => {
    // The tab may still be listing the folder it started in, or waiting on the first page.
    if (currentDirId.value !== directoryId || files.length === 0) return false;

    if (files.some((file) => file.fileId === fileId)) {
      select(fileId);
      void scrollToFile();
      return true;
    }

    if (!hasMoreFiles()) return true;

    loadMoreFiles();
    return false;
  };

  const stopFiles = watch(filesList, (files) => {
    if (check(files)) finish();
  });

  const stopDirectory = watch(currentDirId, (next, previous) => {
    if (previous === directoryId && next !== directoryId) finish();
  });

  const finish = () => {
    stopFiles();
    stopDirectory();
  };

  if (check(filesList.value)) finish();
};
