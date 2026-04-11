import { useQuery, useQueryCache } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { type Ref, computed, ref, watch } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import type { PaginationParams } from "@/types/pagination-params";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { DIRECTORY_QUERY_KEYS, directoryPath, rootDirectories, subDirectories } from "@/queries/directories";
import { FILES_QUERY_KEYS, rootFiles, subFiles } from "@/queries/files";
import { useFileStore } from "@/stores/file";

//oxlint-disable-next-line max-lines-per-function max-statements
export const useFileExplorer = () => {
  const fileStore = useFileStore();
  
    const queryCache = useQueryCache();
  
  // Default to list view on mobile — evaluated once at instantiation time.
  // useMediaQuery is synchronous on the client so .value is correct immediately.
  const isMobileOnInit = useMediaQuery("(max-width: 767px)");
  const viewMode: Ref<"grid" | "list"> = ref(isMobileOnInit.value ? "list" : "grid");

  // STATE
  const currentDirId: Ref<string | null> = ref(null);
  const pathList = ref<{ id: string; name: string }[]>([]);

  const dirPagination: Ref<{
    paginationParams: PaginationParams;
    hasNext: boolean;
  }> = ref({
    hasNext: false,
    paginationParams: {
      SortBy: SortBy.Name,
      page: 1,
      pageSize: 25,
      sortDirection: SortDirection.Asc,
    },
  });

  const filePagination: Ref<{
    paginationParams: PaginationParams;
    hasNext: boolean;
  }> = ref({
    hasNext: false,
    paginationParams: {
      SortBy: SortBy.Name,
      page: 1,
      pageSize: 25,
      sortDirection: SortDirection.Asc,
    },
  });

  const navigationHistory = ref<(string | null)[]>([null]);
  const historyIndex = ref(0);

  const canGoBack = computed(() => historyIndex.value > 0);
  const canGoForward = computed(() => historyIndex.value < navigationHistory.value.length - 1);

  const selectedDirectories: Ref<Set<string>> = ref(new Set<string>());
  const selectedFiles: Ref<Set<string>> = ref(new Set<string>());
  const lastSelected = ref("");
  const directoriesList = ref<DirectorySummaryDto[]>([]);
  const filesList = ref<FileResult[]>([]);

  const pathQuery = useQuery(directoryPath, () => currentDirId.value);

  const directories = useQuery(() => {
    const params = dirPagination.value.paginationParams;
    if (!currentDirId.value) return rootDirectories(params);
    return subDirectories({ id: currentDirId.value, params });
  });

  const files = useQuery(() => {
    const params = filePagination.value.paginationParams;
    if (!currentDirId.value) return rootFiles(params);
    return subFiles({ id: currentDirId.value, params });
  });

  watch(
    directories.data,
    (newData) => {
      if (!newData?.items) return;
      dirPagination.value.hasNext = newData.hasNext ?? false;
      if (dirPagination.value.paginationParams.page === 1) {
        directoriesList.value = [...newData.items];
      } else {
        directoriesList.value.push(...newData.items);
      }
    },
    { immediate: true },
  );

  watch(
    files.data,
    (newData) => {
      if (!newData?.items) return;
      filePagination.value.hasNext = newData.hasNext ?? false;
      if (filePagination.value.paginationParams.page === 1) {
        filesList.value = [...newData.items];
      } else {
        filesList.value.push(...newData.items);
      }
    },
    { immediate: true },
  );

  // ACTIONS

  const navigateTo = (dirId?: string | null, skipHistory = false) => {
    const target = dirId ?? null;
    if (currentDirId.value === target) return;

    if (!skipHistory) {
      navigationHistory.value = navigationHistory.value.slice(0, historyIndex.value + 1);
      navigationHistory.value.push(target);
      historyIndex.value = navigationHistory.value.length - 1;
    }

    dirPagination.value.paginationParams.page = 1;
    filePagination.value.paginationParams.page = 1;
    directoriesList.value = [];
    filesList.value = [];
    currentDirId.value = target;
  };

  const navigateBack = () => {
    if (!canGoBack.value) return;
    historyIndex.value--;
    navigateTo(navigationHistory.value[historyIndex.value], true);
  };

  const navigateForward = () => {
    if (!canGoForward.value) return;
    historyIndex.value++;
    navigateTo(navigationHistory.value[historyIndex.value], true);
  };

  const loadMoreDirs = () => {
    if (!dirPagination.value.hasNext) return;
    dirPagination.value.paginationParams.page++;
  };

  const loadMoreFiles = () => {
    if (!filePagination.value.hasNext) return;
    filePagination.value.paginationParams.page++;
  };

  const refreshDir = () => {
    if (currentDirId.value) {
      queryCache.invalidateQueries({ key: ['directories', 'sub-directories', currentDirId.value] })
      queryCache.invalidateQueries({ key: ['files', 'sub-files', currentDirId.value] })
    } else {
      queryCache.invalidateQueries({ key: [DIRECTORY_QUERY_KEYS.root[0], 'root-sub-directories'] })
      queryCache.invalidateQueries({ key: [FILES_QUERY_KEYS.root[0], 'root-sub-files'] })
    }
  }

  const toggleSelect = (id: string, type: "file" | "directory") =>
    type === "file" ? selectedFiles.value.add(id) : selectedDirectories.value.add(id);

  const setSelection = (ids: string[], type: "file" | "directory") =>
    type === "file"
      ? (selectedFiles.value = new Set(ids))
      : (selectedDirectories.value = new Set(ids));

  const isFileSelected = (id: string) => selectedFiles.value.has(id);
  const isDirectorySelected = (id: string) => selectedDirectories.value.has(id);

  const selectRange = (startId: string, endId: string) => {
    const allItems = [
      ...directoriesList.value.map((d) => ({ id: d.id, type: "directory" as const })),
      ...filesList.value.map((f) => ({ id: f.fileId, type: "file" as const })),
    ];

    const startIndex = allItems.findIndex((item) => item.id === startId);
    const endIndex = allItems.findIndex((item) => item.id === endId);
    if (startIndex === -1 || endIndex === -1) return;

    const [from, to] = startIndex < endIndex ? [startIndex, endIndex] : [endIndex, startIndex];
    const idsToSelect = allItems.slice(from, to + 1);

    const grouped = idsToSelect.reduce(
      (acc, item) => {
        acc[item.type].push(item.id);
        return acc;
      },
      { directory: [] as string[], file: [] as string[] },
    );

    setSelection(grouped.file, "file");
    setSelection(grouped.directory, "directory");
  };

  const downloadFile = async (fileId: string, fileName: string) => {
    await fileStore.downloadFile({ fileName, forceDownload: true, id: fileId });
  };

  const clearSelection = () => {
    selectedFiles.value = new Set<string>();
    selectedDirectories.value = new Set<string>();
  };

  return {
    canGoBack,
    canGoForward,
    clearSelection,
    currentDirId,
    dirPagination,
    directoriesList,
    directoriesQuery: directories,
    downloadFile,
    filePagination,
    filesList,
    filesQuery: files,
    isDirectorySelected,
    isFileSelected,
    lastSelected,
    loadMoreDirs,
    loadMoreFiles,
    navigateBack,
    navigateForward,
    navigateTo,
    pathList,
    pathQuery,
    refreshDir,
    selectRange,
    selectedDirectories,
    selectedFiles,
    setSelection,
    toggleSelect,
    viewMode,
  };
};
