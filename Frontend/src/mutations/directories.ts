import { defineMutation } from "@pinia/colada";

import type {
  CreateDirectorySchema,
  DeleteDirectorySchema,
  UpdateDirectorySchema,
} from "@/schemas/directory";

import { directoryApi } from "@/api/directory";

export const createDirectory = defineMutation({
  mutation: (data: CreateDirectorySchema) => directoryApi.createDirectory(data),
});

export const updateDirectory = defineMutation({
  mutation: (data: UpdateDirectorySchema) => directoryApi.updateDirectory(data),
});

export const moveDirectories = defineMutation({
  mutation: ({
    directoryIds,
    destinationId,
  }: {
    directoryIds: string[];
    destinationId: string | null;
  }) => directoryApi.moveDirectories(directoryIds, destinationId),
});

export const copyDirectory = defineMutation({
  mutation: ({
    directoryId,
    destinationId,
  }: {
    directoryId: string;
    destinationId: string | null;
  }) => directoryApi.copyDirectory(directoryId, destinationId),
});

export const deleteDirectory = defineMutation({
  mutation: ({ id, options = { force: false } }: { id: string; options: DeleteDirectorySchema }) =>
    directoryApi.deleteDirectory(id, options),
});

export const restoreDirectories = defineMutation({
  mutation: (directoryIds: string[]) => directoryApi.restoreDirectories(directoryIds),
});
