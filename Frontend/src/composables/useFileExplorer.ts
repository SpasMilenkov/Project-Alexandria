import { OrderBy } from "@/enums/OrderBy";
import { SortDirection } from "@/enums/SortDirection";
import type { PaginationParams } from "@/types/pagination-params";
import { ref, watch, type Ref } from "vue";
import { useRouter } from "vue-router";
import {
  directoryPath,
  rootDirectories,
  subDirectories,
} from "@/queries/directories";
import { rootFiles, subFiles } from "@/queries/files";
import { useQuery } from "@pinia/colada";
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import { useFileStore } from "@/stores/file";

export const useFileExplorer = () => {
  const router = useRouter();
  const fileStore = useFileStore();
  // STATE
  const currentDirId: Ref<string | null> = ref(null);
  const pathList = ref<{ id: string; name: string }[]>([]);

  //   dirPagination;
  const dirPagination: Ref<{
    paginationParams: PaginationParams;
    hasNext: boolean;
  }> = ref({
    paginationParams: {
      page: 1,
      pageSize: 25,
      orderBy: OrderBy.Name,
      sortDirection: SortDirection.Asc,
    },
    hasNext: false,
  });
  //   filePagination;
  const filePagination: Ref<{
    paginationParams: PaginationParams;
    hasNext: boolean;
  }> = ref({
    paginationParams: {
      page: 1,
      pageSize: 25,
      orderBy: OrderBy.Name,
      sortDirection: SortDirection.Asc,
    },
    hasNext: false,
  });

  const viewMode: Ref<"grid" | "list"> = ref("grid");
  const selectedDirectories: Ref<Set<string>> = ref(new Set<string>());
  const selectedFiles: Ref<Set<string>> = ref(new Set<string>());
  const lastSelected = ref("");
  const directoriesList = ref<DirectorySummaryDto[]>([]);
  const filesList = ref<FileResult[]>([]);

  const pathQuery = useQuery(directoryPath, () => currentDirId.value);

  const directories = useQuery(() => {
    const params = dirPagination.value.paginationParams;

    if (!currentDirId.value) {
      return rootDirectories(params);
    }

    return subDirectories({
      id: currentDirId.value,
      params: params,
    });
  });

  const files = useQuery(() => {
    const params = filePagination.value.paginationParams;

    if (!currentDirId.value) {
      return rootFiles(params);
    }

    return subFiles({
      id: currentDirId.value,
      params: params,
    });
  });

  watch(directories.data, (newData) => {
    if (!newData?.items) return;

    dirPagination.value.hasNext = newData.hasNext ?? false;

    if (dirPagination.value.paginationParams.page === 1) {
      // New folder or refresh: Replace the list
      directoriesList.value = [...newData.items];
    } else {
      // Pagination: Append to the list
      directoriesList.value.push(...newData.items);
    }
  });

  watch(files.data, (newData) => {
    if (!newData?.items) return;

    filePagination.value.hasNext = newData.hasNext ?? false;

    if (filePagination.value.paginationParams.page === 1) {
      filesList.value = [...newData.items];
    } else {
      filesList.value.push(...newData.items);
    }
  });

  // ACTIONS

  const navigateTo = async (dirId?: string | null) => {
    if(currentDirId.value === dirId) return

    dirPagination.value.paginationParams.page = 1;
    filePagination.value.paginationParams.page = 1;

    directoriesList.value = [];
    filesList.value = [];

    currentDirId.value = dirId ?? null;

    router.push({
      name: "dashboard",
      params: { dirId },
    });
  };

  const loadMoreDirs = async () => {
    if (!dirPagination.value.hasNext) return;

    dirPagination.value.paginationParams.page++;
  };

  const loadMoreFiles = async () => {
    if (!filePagination.value.hasNext) return;

    filePagination.value.paginationParams.page++;
  };

  //Since the endless query are still experimental, subject to change and honestly not very intuitive
  //I am forced to do the manual refresh and the watcher stupidity, when the api matures I should
  //use that one instead
  const refreshDir = () => {
    if (dirPagination.value.paginationParams.page === 1) {
      directories.refresh();
      files.refresh();
    } else {
      // Here I could probably do something like a big refetch to avoid having the user click load more 50 times
      //this is fine as is right now
      navigateTo(currentDirId.value);
    }
  };

  const toggleSelect = (id: string, type: "file" | "directory") => {
    return type === "file"
      ? selectedFiles.value.add(id)
      : selectedDirectories.value.add(id);
  };

  const setSelection = (ids: string[], type: "file" | "directory") =>
    type === "file"
      ? (selectedFiles.value = new Set(ids))
      : (selectedDirectories.value = new Set(ids));

  const isFileSelected = (id: string) => selectedFiles.value.has(id);

  const isDirectorySelected = (id: string) => selectedDirectories.value.has(id);

  const selectRange = (startId: string, endId: string) => {
    const allItems = [
      ...directoriesList.value.map((d) => ({
        id: d.id,
        type: "directory" as const,
      })),
      ...filesList.value.map((f) => ({
        id: f.fileId,
        type: "file" as const,
      })),
    ];

    const startIndex = allItems.findIndex((item) => item.id === startId);
    const endIndex = allItems.findIndex((item) => item.id === endId);

    if (startIndex === -1 || endIndex === -1) return;

    const [from, to] =
      startIndex < endIndex ? [startIndex, endIndex] : [endIndex, startIndex];

    const idsToSelect = allItems.slice(from, to + 1);

    const grouped = idsToSelect.reduce(
      (acc, item) => {
        acc[item.type].push(item.id);
        return acc;
      },
      {
        file: [] as string[],
        directory: [] as string[],
      },
    );
    setSelection(grouped.file, "file");
    setSelection(grouped.directory, "directory");
  };

  const downloadFile = async (fileId: string, fileName: string) => {
    await fileStore.downloadFile({
      id: fileId,
      fileName: fileName,
      forceDownload: true,
    });
  };

  const clearSelection = async () => {
    selectedFiles.value = new Set<string>();
    selectedDirectories.value = new Set<string>();
  };

  return {
    currentDirId,
    directoriesQuery: directories,
    filesQuery: files,
    pathQuery,
    directoriesList,
    filesList,
    viewMode,
    filePagination,
    dirPagination,
    pathList,
    lastSelected,
    selectedFiles,
    selectedDirectories,
    navigateTo,
    refreshDir,
    loadMoreDirs,
    loadMoreFiles,
    toggleSelect,
    isFileSelected,
    isDirectorySelected,
    setSelection,
    selectRange,
    downloadFile,
    clearSelection,
  };
};
