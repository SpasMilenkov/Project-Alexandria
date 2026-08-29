import { type QueryCache, defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { fileApi } from "@/api/file";
import { INTEGRATIONS_QUERY_KEYS } from "@/queries/audioAnalysis";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { TAGS_QUERY_KEYS } from "@/queries/tags";
import { type UpdateFileMetadataSchema } from "@/schemas/file";
import { logger } from "@/utils/logger";

/**
 * Invalidates file listings for a specific parent directory.
 * null → root-level listing; string → sub-directory listing.
 * No `exact` so all page/sort variants are hit via prefix match.
 */
const invalidateFileListings = (queryCache: QueryCache, parentId: string | null) => {
  logger.log("Invalidating file listings for parentId", parentId);
  const key =
    parentId === null
      ? [...FILES_QUERY_KEYS.root, "root-sub-files"]
      : [...FILES_QUERY_KEYS.root, "sub-files", parentId];

  queryCache.invalidateQueries({ key });
};

export const updateFileMetadata = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: UpdateFileMetadataSchema) => fileApi.updateFileMetadata(data),
    onSettled(_: any, __: any, data: UpdateFileMetadataSchema) {
      // Only the detail view and the listing that shows its name are stale.
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(data.id) });
      // Title/Artist edits show up in the file drawer's audio-analysis panel.
      queryCache.invalidateQueries({
        exact: true,
        key: INTEGRATIONS_QUERY_KEYS.file(data.id),
      });
      // directoryId of the file is not tracked here, so we invalidate all listings.
      // If you add originId to this mutation's params you can narrow this down.
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
    },
  });
});

export const autoTagFile = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (fileId: string) => fileApi.autoTagFile(fileId),
    onSettled(_: any, __: any, fileId: string) {
      // Tags are derived asynchronously by the worker; invalidate so the drawer's
      // tag list and the file detail refresh once the enrichment lands.
      queryCache.invalidateQueries({ exact: true, key: TAGS_QUERY_KEYS.getTagsForFile(fileId) });
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(fileId) });
    },
  });
});

export const deleteFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      ids,
      hardDelete,
      //oxlint-disable-next-line no-unused-vars
      directoryId,
    }: {
      ids: string[];
      hardDelete?: boolean;
      directoryId?: string;
    }) => fileApi.deleteFiles(ids, hardDelete),
    onSettled(
      _: any,
      __: any,
      { hardDelete, directoryId }: { ids: string[]; hardDelete?: boolean; directoryId?: string },
    ) {
      invalidateFileListings(queryCache, directoryId ?? null);

      if (!hardDelete) {
        queryCache.invalidateQueries({ key: [...FILES_QUERY_KEYS.root, "search-files"] });
      }
    },
  });
});

export const copyFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      fileIds,
      destinationId,
    }: {
      fileIds: string[];
      destinationId: string | null;
      originId: string | null;
    }) => fileApi.copyFiles(fileIds, destinationId),
    onSettled(
      _: any,
      __: any,
      {
        destinationId,
      }: { fileIds: string[]; destinationId: string | null; originId: string | null },
    ) {
      // Origin is unchanged — only the destination gains new entries.
      invalidateFileListings(queryCache, destinationId);
    },
  });
});

export const moveFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      fileIds,
      destinationId,
      // oxlint-disable-next-line no-unused-vars
      originId,
    }: {
      fileIds: string[];
      destinationId: string | null;
      originId: string | null;
    }) => fileApi.moveFiles(fileIds, destinationId),

    onSettled(_data, _error, { originId, destinationId }) {
      const originKey =
        originId === null
          ? [...FILES_QUERY_KEYS.root, "root-sub-files"]
          : [...FILES_QUERY_KEYS.root, "sub-files", originId];

      logger.log("originId:", originId);
      logger.log("origin key:", originKey);
      logger.log("cache entries:", queryCache.getQueryData(originKey));

      invalidateFileListings(queryCache, originId);
      invalidateFileListings(queryCache, destinationId);
    },
  });
});

export const restoreFiles = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (fileIds: string[]) => fileApi.restoreFiles(fileIds),
    onSettled() {
      // We don't know which directories these files were restored to,
      // so we must do a broad invalidation here.
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
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(fileId) });
      queryCache.invalidateQueries({
        key: [...FILES_QUERY_KEYS.root, "versions-for-file", fileId],
      });
    },
  });
});

export const changeActiveVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileId, versionId }: { fileId: string; versionId: string }) =>
      fileApi.changeFileVersion({ fileId, versionId }),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(fileId) });
      // current version changed, the unversioned preview redirect target changed too
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.preview(fileId) });
    },
  });
});

export const restoreFileVersion = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ versionId }: { fileId: string; versionId: string }) =>
      fileApi.restoreVersion(versionId),
    onSettled(_: any, __: any, { fileId }: { fileId: string }) {
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(fileId) });
      queryCache.invalidateQueries({
        key: [...FILES_QUERY_KEYS.root, "versions-for-file", fileId],
      });
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.preview(fileId) });
    },
  });
});
