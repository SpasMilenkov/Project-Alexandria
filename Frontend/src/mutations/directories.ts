import { type QueryCache, defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type {
  CreateDirectorySchema,
  DeleteDirectorySchema,
  UpdateDirectorySchema,
} from "@/schemas/directory";

import { directoryApi } from "@/api/directory";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";

/**
 * Invalidates directory listings for a specific parent.
 * null → root-level listing; string → sub-directory listing.
 * No `exact` so all page/sort variants are hit via prefix match.
 */
const invalidateDirectoryListings = (queryCache: QueryCache, parentId: string | null) => {
  const key =
    parentId === null
      ? [...DIRECTORY_QUERY_KEYS.root, "root-sub-directories"]
      : [...DIRECTORY_QUERY_KEYS.root, "sub-directories", parentId];

  queryCache.invalidateQueries({ key });
};

// Mutations

export const createDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: CreateDirectorySchema) => directoryApi.createDirectory(data),
    onSettled(_: any, __: any, data: CreateDirectorySchema) {
      invalidateDirectoryListings(queryCache, data.parentId ?? null);
    },
  });
});

export const updateDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: UpdateDirectorySchema) => directoryApi.updateDirectory(data),
    onSettled(_: any, __: any, data: UpdateDirectorySchema) {
      queryCache.invalidateQueries({
        exact: true,
        key: DIRECTORY_QUERY_KEYS.byId(data.directoryId),
      });
    },
  });
});

export const moveDirectories = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      directoryIds,
      destinationId,
    }: {
      directoryIds: string[];
      destinationId: string | null;
      originId: string | null;
    }) => directoryApi.moveDirectories(directoryIds, destinationId),
    onSettled(
      _: any,
      __: any,
      {
        directoryIds,
        destinationId,
        originId,
      }: { directoryIds: string[]; destinationId: string | null; originId: string | null },
    ) {
      invalidateDirectoryListings(queryCache, originId);
      invalidateDirectoryListings(queryCache, destinationId);

      for (const id of directoryIds) {
        queryCache.invalidateQueries({ exact: true, key: DIRECTORY_QUERY_KEYS.byId(id) });
        queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.directoryPath(id) });
      }
    },
  });
});

export const copyDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      directoryId,
      destinationId,
    }: {
      directoryId: string;
      destinationId: string | null;
      originId: string | null;
    }) => directoryApi.copyDirectory(directoryId, destinationId),
    onSettled(
      _: any,
      __: any,
      {
        destinationId,
      }: { directoryId: string; destinationId: string | null; originId: string | null },
    ) {
      invalidateDirectoryListings(queryCache, destinationId);
    },
  });
});

export const deleteDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      id,
      options = { force: false },
    }: {
      id: string;
      options: DeleteDirectorySchema;
      originId: string | null;
    }) => directoryApi.deleteDirectory(id, options),
    onSettled(
      _: any,
      __: any,
      { id, originId }: { id: string; options: DeleteDirectorySchema; originId: string | null },
    ) {
      invalidateDirectoryListings(queryCache, originId);
      queryCache.invalidateQueries({ exact: true, key: DIRECTORY_QUERY_KEYS.byId(id) });
    },
  });
});

export const restoreDirectories = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (directoryIds: string[]) => directoryApi.restoreDirectories(directoryIds),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
    },
  });
});
