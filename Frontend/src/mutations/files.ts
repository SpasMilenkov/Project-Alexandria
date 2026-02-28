import { defineMutation } from "@pinia/colada";

import { fileApi } from "@/api/file";
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
