import { createPinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { type Ref, nextTick, ref, createApp } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

import { useFileExplorer } from "@/composables/useFileExplorer";

// Mocks

const mockRouterPush = vi.fn();
vi.mock("vue-router", () => ({
  useRouter: () => ({ push: mockRouterPush }),
}));

vi.mock("@/stores/file", () => ({
  useFileStore: () => ({ downloadFile: vi.fn() }),
}));

// useQuery is called 3 times in order: pathQuery → directories → files
// We capture the mock so we can swap implementations per-test in beforeEach
const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

// The actual query definitions don't matter here; we only care about what
// useQuery returns, not what gets passed into it
vi.mock("@/queries/directories", () => ({
  directoryPath: Symbol("directoryPath"),
  rootDirectories: vi.fn(),
  subDirectories: vi.fn(),
}));

vi.mock("@/queries/files", () => ({
  rootFiles: vi.fn(),
  subFiles: vi.fn(),
}));

// Test helpers

/**
 * Run a composable inside a real Vue + Pinia app so that reactivity,
 * computed values, and watchers all work exactly as they would in production.
 */
const withSetup = <T>(composable: () => T): T => {
  let result!: T;
  const app = createApp({
    setup() {
      result = composable();
      return () => null;
    },
  });
  app.use(createPinia());
  app.mount(document.createElement("div"));
  return result;
};

const makeDir = (id: string): DirectorySummaryDto => ({
  createdAt: new Date().toISOString(),
  id,
  name: `Dir ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: "root",
  updatedAt: new Date().toISOString(),
});

const makeFile = (fileId: string): FileResult => ({
  createdAt: new Date().toISOString(),
  currentVersion: {
    id: "v1",
    isDeleted: false,
    mimeType: "text/plain",
    size: "1024",
    versionNumber: 1,
    isEncrypted: false,
  },
  deletedAt: null,
  directoryId: null,
  fileId,
  fileName: `file-${fileId}.txt`,
  mimeType: "text/plain",
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
});

// Tests
//oxlint-disable-next-line max-lines-per-function
describe("useFileExplorer", () => {
  let mockDirData: Ref<{
    items: DirectorySummaryDto[];
    hasNext: boolean;
  } | null>;
  let mockFilesData: Ref<{ items: FileResult[]; hasNext: boolean } | null>;

  beforeEach(() => {
    mockRouterPush.mockClear();
    mockDirData = ref(null);
    mockFilesData = ref(null);

    // Re-implement per test so the call counter resets each time
    let callCount = 0;
    mockUseQuery.mockImplementation(() => {
      callCount++;
      if (callCount === 1) return { data: ref(null), refresh: vi.fn() }; // pathQuery
      if (callCount === 2) return { data: mockDirData, refresh: vi.fn() }; // directories
      if (callCount === 3) return { data: mockFilesData, refresh: vi.fn() }; // files
      return { data: ref(null), refresh: vi.fn() };
    });
  });

  // toggleSelect

  describe("toggleSelect", () => {
    it("adds a file id to selectedFiles", () => {
      const { toggleSelect, selectedFiles } = withSetup(() => useFileExplorer());
      toggleSelect("file-1", "file");
      expect(selectedFiles.value.has("file-1")).toBe(true);
    });

    it("adds a directory id to selectedDirectories", () => {
      const { toggleSelect, selectedDirectories } = withSetup(() => useFileExplorer());
      toggleSelect("dir-1", "directory");
      expect(selectedDirectories.value.has("dir-1")).toBe(true);
    });

    it("does not bleed a file id into selectedDirectories", () => {
      const { toggleSelect, selectedDirectories } = withSetup(() => useFileExplorer());
      toggleSelect("file-1", "file");
      expect(selectedDirectories.value.has("file-1")).toBe(false);
    });

    it("does not bleed a directory id into selectedFiles", () => {
      const { toggleSelect, selectedFiles } = withSetup(() => useFileExplorer());
      toggleSelect("dir-1", "directory");
      expect(selectedFiles.value.has("dir-1")).toBe(false);
    });
  });

  // setSelection

  describe("setSelection", () => {
    it("replaces selectedFiles entirely (does not merge)", () => {
      const { toggleSelect, setSelection, selectedFiles } = withSetup(() => useFileExplorer());
      toggleSelect("old-file", "file");

      setSelection(["new-1", "new-2"], "file");

      expect(selectedFiles.value.has("old-file")).toBe(false);
      expect(selectedFiles.value.has("new-1")).toBe(true);
      expect(selectedFiles.value.has("new-2")).toBe(true);
    });

    it("replaces selectedDirectories entirely (does not merge)", () => {
      const { toggleSelect, setSelection, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );
      toggleSelect("old-dir", "directory");

      setSelection(["new-dir-1"], "directory");

      expect(selectedDirectories.value.has("old-dir")).toBe(false);
      expect(selectedDirectories.value.has("new-dir-1")).toBe(true);
    });

    it("does not touch selectedFiles when replacing directories", () => {
      const { toggleSelect, setSelection, selectedFiles } = withSetup(() => useFileExplorer());
      toggleSelect("f1", "file");
      setSelection(["d1"], "directory");
      expect(selectedFiles.value.has("f1")).toBe(true);
    });
  });

  // isFileSelected / isDirectorySelected

  describe("isFileSelected / isDirectorySelected", () => {
    it("returns true for a selected file", () => {
      const { toggleSelect, isFileSelected } = withSetup(() => useFileExplorer());
      toggleSelect("f1", "file");
      expect(isFileSelected("f1")).toBe(true);
    });

    it("returns false for a file that has not been selected", () => {
      const { isFileSelected } = withSetup(() => useFileExplorer());
      expect(isFileSelected("f1")).toBe(false);
    });

    it("returns true for a selected directory", () => {
      const { toggleSelect, isDirectorySelected } = withSetup(() => useFileExplorer());
      toggleSelect("d1", "directory");
      expect(isDirectorySelected("d1")).toBe(true);
    });

    it("returns false for a directory that has not been selected", () => {
      const { isDirectorySelected } = withSetup(() => useFileExplorer());
      expect(isDirectorySelected("d1")).toBe(false);
    });
  });

  // clearSelection

  describe("clearSelection", () => {
    it("empties both selectedFiles and selectedDirectories", () => {
      const { toggleSelect, clearSelection, selectedFiles, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      toggleSelect("f1", "file");
      toggleSelect("d1", "directory");
      clearSelection();

      expect(selectedFiles.value.size).toBe(0);
      expect(selectedDirectories.value.size).toBe(0);
    });

    it("is safe to call when nothing is selected", () => {
      const { clearSelection, selectedFiles, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      clearSelection();
      expect(selectedFiles.value.size).toBe(0);
      expect(selectedDirectories.value.size).toBe(0);
    });
  });

  // selectRange

  describe("selectRange", () => {
    it("selects a contiguous range of directories in order", () => {
      const { directoriesList, selectRange, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      directoriesList.value = [makeDir("d1"), makeDir("d2"), makeDir("d3")];
      selectRange("d1", "d3");

      expect(selectedDirectories.value.has("d1")).toBe(true);
      expect(selectedDirectories.value.has("d2")).toBe(true);
      expect(selectedDirectories.value.has("d3")).toBe(true);
    });

    it("selects a contiguous range of files in order", () => {
      const { filesList, selectRange, selectedFiles } = withSetup(() => useFileExplorer());

      filesList.value = [makeFile("f1"), makeFile("f2"), makeFile("f3")];
      selectRange("f1", "f3");

      expect(selectedFiles.value.has("f1")).toBe(true);
      expect(selectedFiles.value.has("f2")).toBe(true);
      expect(selectedFiles.value.has("f3")).toBe(true);
    });

    it("handles reversed selection (end id comes before start id in the list)", () => {
      const { directoriesList, selectRange, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      directoriesList.value = [makeDir("d1"), makeDir("d2"), makeDir("d3")];
      // User shift-clicked upward
      selectRange("d3", "d1");

      expect(selectedDirectories.value.has("d1")).toBe(true);
      expect(selectedDirectories.value.has("d2")).toBe(true);
      expect(selectedDirectories.value.has("d3")).toBe(true);
    });

    it("selects a range that spans across directories and files", () => {
      // Flat order: d1, d2, f1, f2
      const { directoriesList, filesList, selectRange, selectedDirectories, selectedFiles } =
        withSetup(() => useFileExplorer());

      directoriesList.value = [makeDir("d1"), makeDir("d2")];
      filesList.value = [makeFile("f1"), makeFile("f2")];
      selectRange("d2", "f1");

      expect(selectedDirectories.value.has("d2")).toBe(true);
      expect(selectedFiles.value.has("f1")).toBe(true);
      // d1 is outside the range and should be untouched
      expect(selectedDirectories.value.has("d1")).toBe(false);
      expect(selectedFiles.value.has("f2")).toBe(false);
    });

    it("selects a single item when start and end are the same", () => {
      const { directoriesList, selectRange, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      directoriesList.value = [makeDir("d1"), makeDir("d2"), makeDir("d3")];
      selectRange("d2", "d2");

      expect(selectedDirectories.value.has("d2")).toBe(true);
      expect(selectedDirectories.value.size).toBe(1);
    });

    it("is a no-op when the start id does not exist", () => {
      const { directoriesList, selectRange, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      directoriesList.value = [makeDir("d1"), makeDir("d2")];
      selectRange("ghost", "d2");

      expect(selectedDirectories.value.size).toBe(0);
    });

    it("is a no-op when the end id does not exist", () => {
      const { directoriesList, selectRange, selectedDirectories } = withSetup(() =>
        useFileExplorer(),
      );

      directoriesList.value = [makeDir("d1"), makeDir("d2")];
      selectRange("d1", "ghost");

      expect(selectedDirectories.value.size).toBe(0);
    });

    it("replaces any prior selection with the new range", () => {
      const { filesList, selectRange, selectedFiles } = withSetup(() => useFileExplorer());

      filesList.value = [makeFile("f1"), makeFile("f2"), makeFile("f3")];
      selectRange("f1", "f3");
      // Now narrow the selection
      selectRange("f2", "f2");

      expect(selectedFiles.value.has("f1")).toBe(false);
      expect(selectedFiles.value.has("f2")).toBe(true);
      expect(selectedFiles.value.has("f3")).toBe(false);
    });
  });

  // directories watcher

  describe("directories watcher", () => {
    it("replaces directoriesList when page is 1", async () => {
      const { directoriesList, dirPagination } = withSetup(() => useFileExplorer());

      dirPagination.value.paginationParams.page = 1;
      mockDirData.value = {
        hasNext: false,
        items: [makeDir("d1"), makeDir("d2")],
      };
      await nextTick();

      expect(directoriesList.value).toHaveLength(2);
      expect(directoriesList.value[0].id).toBe("d1");
    });

    it("appends to directoriesList when page is > 1", async () => {
      const { directoriesList, dirPagination } = withSetup(() => useFileExplorer());

      dirPagination.value.paginationParams.page = 1;
      mockDirData.value = { items: [makeDir("d1")], hasNext: true };
      await nextTick();

      // Simulate user hitting "load more" — page increments before data arrives
      dirPagination.value.paginationParams.page = 2;
      mockDirData.value = { items: [makeDir("d2")], hasNext: false };
      await nextTick();

      expect(directoriesList.value).toHaveLength(2);
      expect(directoriesList.value.map((d) => d.id)).toEqual(["d1", "d2"]);
    });

    it("replaces (does not append) when navigating to a new folder resets page to 1", async () => {
      const { directoriesList, dirPagination } = withSetup(() => useFileExplorer());

      dirPagination.value.paginationParams.page = 1;
      mockDirData.value = {
        hasNext: false,
        items: [makeDir("old1"), makeDir("old2")],
      };
      await nextTick();

      // Navigation resets the page back to 1
      dirPagination.value.paginationParams.page = 1;
      mockDirData.value = { hasNext: false, items: [makeDir("new1")] };
      await nextTick();

      expect(directoriesList.value).toHaveLength(1);
      expect(directoriesList.value[0].id).toBe("new1");
    });

    it("updates the hasNext flag", async () => {
      const { dirPagination } = withSetup(() => useFileExplorer());

      dirPagination.value.paginationParams.page = 1;
      mockDirData.value = { hasNext: true, items: [makeDir("d1")] };
      await nextTick();
      expect(dirPagination.value.hasNext).toBe(true);

      mockDirData.value = { hasNext: false, items: [makeDir("d2")] };
      await nextTick();
      expect(dirPagination.value.hasNext).toBe(false);
    });

    it("does nothing when data is null", async () => {
      const { directoriesList } = withSetup(() => useFileExplorer());
      mockDirData.value = null;
      await nextTick();
      expect(directoriesList.value).toHaveLength(0);
    });

    it("does nothing when items is missing from the payload", async () => {
      const { directoriesList } = withSetup(() => useFileExplorer());
      // @ts-expect-error – intentionally malformed to test the guard
      mockDirData.value = { hasNext: false };
      await nextTick();
      expect(directoriesList.value).toHaveLength(0);
    });
  });

  // files watcher

  describe("files watcher", () => {
    it("replaces filesList when page is 1", async () => {
      const { filesList, filePagination } = withSetup(() => useFileExplorer());

      filePagination.value.paginationParams.page = 1;
      mockFilesData.value = {
        hasNext: false,
        items: [makeFile("f1"), makeFile("f2")],
      };
      await nextTick();

      expect(filesList.value).toHaveLength(2);
      expect(filesList.value[0].fileId).toBe("f1");
    });

    it("appends to filesList when page is > 1", async () => {
      const { filesList, filePagination } = withSetup(() => useFileExplorer());

      filePagination.value.paginationParams.page = 1;
      mockFilesData.value = { hasNext: true, items: [makeFile("f1")] };
      await nextTick();

      filePagination.value.paginationParams.page = 2;
      mockFilesData.value = { hasNext: false, items: [makeFile("f2")] };
      await nextTick();

      expect(filesList.value).toHaveLength(2);
      expect(filesList.value.map((f) => f.fileId)).toEqual(["f1", "f2"]);
    });

    it("updates the hasNext flag for files", async () => {
      const { filePagination } = withSetup(() => useFileExplorer());

      filePagination.value.paginationParams.page = 1;
      mockFilesData.value = { hasNext: true, items: [] };
      await nextTick();
      expect(filePagination.value.hasNext).toBe(true);

      mockFilesData.value = { hasNext: false, items: [] };
      await nextTick();
      expect(filePagination.value.hasNext).toBe(false);
    });

    it("does nothing when data is null", async () => {
      const { filesList } = withSetup(() => useFileExplorer());
      mockFilesData.value = null;
      await nextTick();
      expect(filesList.value).toHaveLength(0);
    });
  });

  // loadMoreDirs

  describe("loadMoreDirs", () => {
    it("increments the directory page when hasNext is true", () => {
      const { loadMoreDirs, dirPagination } = withSetup(() => useFileExplorer());
      dirPagination.value.hasNext = true;
      const before = dirPagination.value.paginationParams.page;

      loadMoreDirs();

      expect(dirPagination.value.paginationParams.page).toBe(before + 1);
    });

    it("does not increment the directory page when hasNext is false", () => {
      const { loadMoreDirs, dirPagination } = withSetup(() => useFileExplorer());
      dirPagination.value.hasNext = false;
      const before = dirPagination.value.paginationParams.page;

      loadMoreDirs();

      expect(dirPagination.value.paginationParams.page).toBe(before);
    });
  });

  // loadMoreFiles

  describe("loadMoreFiles", () => {
    it("increments the file page when hasNext is true", () => {
      const { loadMoreFiles, filePagination } = withSetup(() => useFileExplorer());
      filePagination.value.hasNext = true;
      const before = filePagination.value.paginationParams.page;

      loadMoreFiles();

      expect(filePagination.value.paginationParams.page).toBe(before + 1);
    });

    it("does not increment the file page when hasNext is false", () => {
      const { loadMoreFiles, filePagination } = withSetup(() => useFileExplorer());
      filePagination.value.hasNext = false;
      const before = filePagination.value.paginationParams.page;

      loadMoreFiles();

      expect(filePagination.value.paginationParams.page).toBe(before);
    });
  });
});
