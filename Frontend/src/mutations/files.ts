import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { fileApi } from "@/api/file";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { type UpdateFileMetadataSchema } from "@/schemas/file";

export const updateFileMetadata = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ id, data }: { id: string; data: UpdateFileMetadataSchema }) =>
      fileApi.updateFileMetadata(id, data),
    onSettled(_: any, __: any, { id }: { id: string }) {
      // Exact: only this file's detail changed, not listings
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(id), exact: true });
      // But listings may show the updated name, so also invalidate those
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const deleteFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ ids, hardDelete }: { ids: string[]; hardDelete?: boolean }) =>
      fileApi.deleteFiles(ids, hardDelete),
    onSettled() {
      // Prefix match: invalidates rootFiles, subFiles, searchFiles — all variants
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const copyFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileIds, destinationId }: { fileIds: string[]; destinationId: string | null }) =>
      fileApi.copyFiles(fileIds, destinationId),
    onSettled() {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const moveFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileIds, destinationId }: { fileIds: string[]; destinationId: string | null }) =>
      fileApi.moveFiles(fileIds, destinationId),
    onSettled() {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const restoreFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (fileIds: string[]) => fileApi.restoreFiles(fileIds),
    onSettled() {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const deleteVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ versionId }: { fileId: string; versionId: string }) =>
      fileApi.deleteFileVersion(versionId),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(fileId), exact: true });
    },
  });
});

export const changeActiveVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileId, versionId }: { fileId: string; versionId: string }) =>
      fileApi.changeFileVersion({ fileId, versionId }),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(fileId), exact: true });
    },
  });
});

export const restoreFileVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ versionId }: { fileId: string; versionId: string }) =>
      fileApi.restoreVersion(versionId),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.getFile(fileId), exact: true });
      // Invalidate all version pages for this file — no exact so all page/size combos are hit
      queryCache.invalidateQueries({
        key: [...FILES_QUERY_KEYS.root, "versions-for-file", fileId],
      });
    },
  });
});