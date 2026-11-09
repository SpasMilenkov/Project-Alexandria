import { describe, expect, it, vi } from "vitest";
import { type Ref, nextTick, ref } from "vue";

import type { FileResult } from "@/api/file";

import { useExplorerReveal } from "@/composables/useExplorerReveal";

const file = (fileId: string) => ({ fileId }) as FileResult;

interface Setup {
  currentDirId: Ref<string | null>;
  filesList: Ref<FileResult[]>;
  hasNext: Ref<boolean>;
  loadMoreFiles: ReturnType<typeof vi.fn>;
  select: ReturnType<typeof vi.fn>;
}

const setup = (startDir: string | null = "folder"): Setup => {
  const currentDirId = ref<string | null>(startDir);
  const filesList = ref<FileResult[]>([]);
  const hasNext = ref(false);
  const loadMoreFiles = vi.fn();
  const select = vi.fn();

  useExplorerReveal({
    fileId: "target",
    directoryId: "folder",
    currentDirId,
    filesList,
    hasMoreFiles: () => hasNext.value,
    loadMoreFiles,
    select,
  });

  return { currentDirId, filesList, hasNext, loadMoreFiles, select };
};

describe("useExplorerReveal", () => {
  it("selects the file once the folder lists it", async () => {
    const { filesList, select } = setup();

    filesList.value = [file("other"), file("target")];
    await nextTick();

    expect(select).toHaveBeenCalledWith("target");
  });

  it("loads the next page while the file is not listed yet", async () => {
    const { filesList, hasNext, loadMoreFiles, select } = setup();

    hasNext.value = true;
    filesList.value = [file("other")];
    await nextTick();

    expect(loadMoreFiles).toHaveBeenCalledOnce();
    expect(select).not.toHaveBeenCalled();

    filesList.value = [file("other"), file("target")];
    await nextTick();

    expect(select).toHaveBeenCalledWith("target");
  });

  it("gives up when the folder has no more pages", async () => {
    const { filesList, hasNext, loadMoreFiles, select } = setup();

    filesList.value = [file("other")];
    await nextTick();

    hasNext.value = true;
    filesList.value = [file("other"), file("target")];
    await nextTick();

    expect(loadMoreFiles).not.toHaveBeenCalled();
    expect(select).not.toHaveBeenCalled();
  });

  it("ignores listings from the folder the tab started in", async () => {
    const { currentDirId, filesList, hasNext, loadMoreFiles, select } = setup(null);

    hasNext.value = true;
    filesList.value = [file("root-file")];
    await nextTick();

    expect(loadMoreFiles).not.toHaveBeenCalled();

    currentDirId.value = "folder";
    filesList.value = [file("target")];
    await nextTick();

    expect(select).toHaveBeenCalledWith("target");
  });

  it("stops once the user leaves the folder", async () => {
    const { currentDirId, filesList, select } = setup();

    currentDirId.value = "elsewhere";
    await nextTick();

    currentDirId.value = "folder";
    filesList.value = [file("target")];
    await nextTick();

    expect(select).not.toHaveBeenCalled();
  });
});
