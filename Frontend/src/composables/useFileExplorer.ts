import { type DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import { OrderBy } from "@/enums/OrderBy";
import { SortDirection } from "@/enums/SortDirection";
import type { PaginationParams } from "@/types/pagination-params";
import { ref, type Ref } from "vue";
import { useFileExplorerApi } from "./useFileExplorerApi";
import { useRouter } from "vue-router";
import { useDirectoryStore } from "@/stores/directory";

export const useFileExplorer = () => {
  const router = useRouter();
  const directoryStore = useDirectoryStore();
  // STATE
  const currentDirId: Ref<string | null> = ref(null);
  const pathList = ref<{ id: string; name: string }[]>([]);

  //   directories;
  const directories = ref<DirectorySummaryDto[]>([]);
  //   files;
  const files = ref<FileResult[]>([]);
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
  //   sort;
  //   viewMode;
  const viewMode: Ref<"grid" | "list"> = ref("grid");
  //   selection;
  const selectedDirectories: Ref<Set<string>> = ref(new Set<string>());
  const selectedFiles: Ref<Set<string>> = ref(new Set<string>());
  const lastSelected = ref("");
  //   loading;
  const isLoading = ref(false);
  //   error;
  const error: Ref<string | null> = ref();

  const {
    getRootSubDirectories,
    getRootSubFiles,
    getSubDirectories,
    getSubFiles,
    fileDownload,
    getDirectoryPath,
  } = useFileExplorerApi();

  // ACTIONS

  const navigateTo = async (dirId?: string | null) => {
    dirPagination.value = {
      paginationParams: {
        page: 1,
        pageSize: 25,
        orderBy: OrderBy.Name,
        sortDirection: SortDirection.Asc,
      },
      hasNext: true,
    };

    filePagination.value = {
      paginationParams: {
        page: 1,
        pageSize: 25,
        orderBy: OrderBy.Name,
        sortDirection: SortDirection.Asc,
      },
      hasNext: true,
    };

    currentDirId.value = dirId ?? null;
    if (!dirId) {
      router.push({
        name: "dashboard",
      });
      const directoriesResult = await getRootSubDirectories(
        dirPagination.value.paginationParams
      );

      if (directoriesResult) {
        directories.value = directoriesResult.data
          ?.items as DirectorySummaryDto[];
        dirPagination.value.hasNext = directoriesResult?.data?.hasNext ?? false;
      }

      const filesResult = await getRootSubFiles(
        filePagination.value.paginationParams
      );
      if (filesResult) files.value = filesResult.data?.items as FileResult[];
      filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;

      updateDirectoryPath();
      return;
    }
    updateDirectoryPath(dirId);

    const directoriesResult = await getSubDirectories(
      dirId,
      dirPagination.value.paginationParams
    );

    if (directoriesResult) {
      directories.value = directoriesResult.data
        ?.items as DirectorySummaryDto[];

      dirPagination.value.hasNext = directoriesResult.data?.hasNext ?? false;
    }

    const filesResult = await getSubFiles(
      dirId,
      filePagination.value.paginationParams
    );
    if (filesResult) {
      files.value = filesResult.data?.items as FileResult[];
      filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;
    }
    router.push({
      name: "dashboard",
      params: { dirId },
    });
    directoryStore.navigationHistory.push(currentDirId.value);
  };

  const refreshDir = async (dirId?: string | null) => {
    if (!dirId) {
      router.push({
        name: "dashboard",
      });
      const directoriesResult = await getRootSubDirectories(
        dirPagination.value.paginationParams
      );

      if (directoriesResult) {
        directories.value = directoriesResult.data
          ?.items as DirectorySummaryDto[];
        dirPagination.value.hasNext = directoriesResult?.data?.hasNext ?? false;
      }

      const filesResult = await getRootSubFiles(
        filePagination.value.paginationParams
      );
      if (filesResult) files.value = filesResult.data?.items as FileResult[];
      filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;

      updateDirectoryPath();
      return;
    }
    updateDirectoryPath(dirId);

    const directoriesResult = await getSubDirectories(
      dirId,
      dirPagination.value.paginationParams
    );

    if (directoriesResult)
      directories.value = directoriesResult.data
        ?.items as DirectorySummaryDto[];

    const filesResult = await getSubFiles(
      dirId,
      filePagination.value.paginationParams
    );
    if (filesResult) {
      files.value = filesResult.data?.items as FileResult[];
      filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;
    }
    router.push({
      name: "dashboard",
      params: { dirId },
    });
    directoryStore.navigationHistory.push(currentDirId.value);
  };
  const loadMoreDirs = async () => {
    if (!dirPagination.value.hasNext) return;

    dirPagination.value.paginationParams.page++;
    if (!currentDirId.value) {
      const directoriesResult = await getRootSubDirectories(
        dirPagination.value.paginationParams
      );

      if (directoriesResult)
        directories.value.push(
          ...(directoriesResult.data?.items as DirectorySummaryDto[])
        );

      return;
    }
    const directoriesResult = await getSubDirectories(
      currentDirId.value,
      dirPagination.value.paginationParams
    );

    if (directoriesResult?.success)
      directories.value.push(
        ...(directoriesResult.data?.items as DirectorySummaryDto[])
      );
  };

  const loadMoreFiles = async () => {
    if (!filePagination.value.hasNext) return;
    console.log("loading files");
    filePagination.value.paginationParams.page++;

    if (!currentDirId.value) {
      const filesResult = await getRootSubFiles(
        filePagination.value.paginationParams
      );

      if (filesResult?.success && files.value) {
        files.value.push(...(filesResult.data?.items as FileResult[]));
        filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;
      }
      return;
    }
    const filesResult = await getSubFiles(
      currentDirId.value,
      filePagination.value.paginationParams
    );
    if (filesResult?.success && files.value) {
      files.value.push(...(filesResult.data?.items as FileResult[]));
      filePagination.value.hasNext = filesResult?.data?.hasNext ?? false;
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
      ...directories.value.map((d) => ({
        id: d.id,
        type: "directory" as const,
      })),
      ...files.value.map((f) => ({ id: f.fileId, type: "file" as const })),
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
      }
    );
    setSelection(grouped.file, "file");
    setSelection(grouped.directory, "directory");
  };

  const downloadFile = async (fileId: string, fileName: string) => {
    fileDownload({
      id: fileId,
      fileName: fileName,
      forceDownload: true,
    });
  };

  const clearSelection = async () => {
    selectedFiles.value = new Set<string>();
    selectedDirectories.value = new Set<string>();
  };

  const updateDirectoryPath = async (id?: string) => {
    if (!id) return (pathList.value = []);
    pathList.value = (await getDirectoryPath(id)) ?? [];
  };
  return {
    currentDirId,
    directories,
    files,
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
    updateDirectoryPath,
  };
};
