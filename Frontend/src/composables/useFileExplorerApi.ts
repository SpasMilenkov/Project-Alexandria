import { useDirectoryStore } from "@/stores/directory";
import { useFileStore } from "@/stores/file";
import type { PaginationParams } from "@/types/pagination-params";

export const useFileExplorerApi = () => {
  const directoryStore = useDirectoryStore();
  const fileStore = useFileStore();

  const getSubDirectories = async (
    dirId: string,
    paginationParams: PaginationParams
  ) => {
    const result = await directoryStore.getSubDirectories(
      dirId,
      paginationParams
    );
    if (result.success) return result;
  };

  const getRootSubDirectories = async (paginationParams: PaginationParams) => {
    const result = await directoryStore.getRooSubDirectories(paginationParams);
    if (result.success) return result;
  };
  const getSubFiles = async (
    dirId: string,
    paginationParams: PaginationParams
  ) => {
    const result = await fileStore.getSubFiles(dirId, paginationParams);
    if (result.success) return result;
  };

  const getRootSubFiles = async (paginationParams: PaginationParams) => {
    const result = await fileStore.getRootFiles(paginationParams);
    if (result.success) return result;
  };

  const fileDownload = async (fileData: {
    id: string;
    fileName: string;
    forceDownload: boolean;
  }) => {
    await fileStore.downloadFile(fileData);
  };

  const fileUpload = async () => {};

  const getDirectoryPath = async (id: string) => {
    const result = await directoryStore.getDirectoryPath(id);
    if (result.success && result.data) return result.data;
  };

  const getFilePreview = async (id: string) => {
    const result = await fileStore.getFilePreview(id);
    console.log(result);
    if (!fileStore.error) return result;
  };

  type CopySelectedParams = {
    destinationId: string | null;
    selectedDirectories?: string[];
    selectedFiles?: string[];
  };

  const copySelected = async ({
    destinationId,
    selectedDirectories,
    selectedFiles,
  }: CopySelectedParams) => {
    if (selectedDirectories) {
      for (const dirId of selectedDirectories) {
        await directoryStore.copyDirectory(dirId, destinationId);
      }
    }

    if (selectedFiles) {
      await fileStore.copyFiles(selectedFiles, destinationId);
    }
  };

  const moveSelected = async ({
    destinationId,
    selectedDirectories,
    selectedFiles,
  }: CopySelectedParams) => {
    if (selectedDirectories) {
      await directoryStore.moveDirectories(selectedDirectories, destinationId);
    }

    if (selectedFiles) {
      await fileStore.moveFiles(selectedFiles, destinationId);
    }
  };
  const deleteDirectory = async (id: string) => {
    await directoryStore.deleteDirectory(id);
  };

  const deleteFile = async (ids: string[]) => {
    await fileStore.deleteFile(ids);
  };

  return {
    getSubDirectories,
    getRootSubDirectories,
    getRootSubFiles,
    getSubFiles,
    getFilePreview,
    fileDownload,
    fileUpload,
    getDirectoryPath,
    copySelected,
    moveSelected,
    deleteFile,
    deleteDirectory
  };
};
