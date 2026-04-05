import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type {
  CreateDirectorySchema,
  DeleteDirectorySchema,
  UpdateDirectorySchema,
} from "@/schemas/directory";

import { directoryApi } from "@/api/directory";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";

export const createDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: CreateDirectorySchema) => directoryApi.createDirectory(data),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
    },
  });
});

export const updateDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: UpdateDirectorySchema) => directoryApi.updateDirectory(data),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
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
    }) => directoryApi.moveDirectories(directoryIds, destinationId),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
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
    }) => directoryApi.copyDirectory(directoryId, destinationId),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
    },
  });
});

export const deleteDirectory = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ id, options = { force: false } }: { id: string; options: DeleteDirectorySchema }) =>
      directoryApi.deleteDirectory(id, options),
    onSettled() {
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
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