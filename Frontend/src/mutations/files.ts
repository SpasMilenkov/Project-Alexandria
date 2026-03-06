import { defineMutation, useQueryCache } from "@pinia/colada";

import { fileApi } from "@/api/file";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { type UpdateFileMetadataSchema } from "@/schemas/file";

export const updateFileMetadata = defineMutation(() => ({
  mutation: ({ id, data }: { id: string; data: UpdateFileMetadataSchema }) =>
    fileApi.updateFileMetadata(id, data),
}));

export const deleteFiles = defineMutation({
  mutation: ({ ids, hardDelete }: { ids: string[]; hardDelete?: boolean }) =>
    fileApi.deleteFiles(ids, hardDelete),
});

export const copyFiles = defineMutation({
  mutation: ({ fileIds, destinationId }: { fileIds: string[]; destinationId: string | null }) =>
    fileApi.copyFiles(fileIds, destinationId),
});

export const moveFiles = defineMutation({
  mutation: ({ fileIds, destinationId }: { fileIds: string[]; destinationId: string | null }) =>
    fileApi.moveFiles(fileIds, destinationId),
});

export const restoreFiles = defineMutation({
  mutation: (fileIds: string[]) => fileApi.restoreFiles(fileIds),
});

export const deleteVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return {
    mutation: ({ versionId }: { versionId: string }) => fileApi.deleteFileVersion(versionId),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(fileId) });
    },
  };
});

export const changeActiveVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return {
    mutation: ({ fileId, versionId }: { fileId: string; versionId: string }) =>
      fileApi.changeFileVersion({ fileId, versionId }),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(fileId) });
    },
  };
});
