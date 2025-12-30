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
    const result = await directoryStore.getDirectoryPath(id)
    if(result.success && result.data)
      return result.data
  }

  const getFilePreview = async (id: string) => {
    const result = await fileStore.getFilePreview(id)
    console.log(result)
    if(!fileStore.error) return result
  }
  const moveDirectory = async () => {};

  const copyDirectory = async () => {};

  const openFile = async () => {};

  const getFileVersions = async (fileId: string) => {};

  const getFileVersionById = async (fileId: string) => {};

  const createDirectory = async () => {};

  const renameDirectory = async () => {};

  const deleteDirectory = async () => {};

  const deleteFile = async () => {};

  const searchParentDirectories = async () => {};

  return {
    getSubDirectories,
    getRootSubDirectories,
    getRootSubFiles,
    getSubFiles,
    getFilePreview,
    fileDownload,
    fileUpload,
    getDirectoryPath,
  };
};
