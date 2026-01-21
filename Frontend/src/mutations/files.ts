import { fileApi } from "@/api/file";
import { type UpdateFileMetadataSchema } from "@/schemas/file";
import { defineMutation } from "@pinia/colada";

export const updateFileMetadata = defineMutation(() => ({
  mutation: ({ id, data }: { id: string; data: UpdateFileMetadataSchema }) =>
    fileApi.updateFileMetadata(id, data),
}));

export const deleteFiles = defineMutation({
  mutation: (ids: string[]) => fileApi.deleteFiles(ids),
});

export const copyFiles = defineMutation({
  mutation: ({
    fileIds,
    destinationId,
  }: {
    fileIds: string[];
    destinationId?: string | null;
  }) => fileApi.copyFiles(fileIds, destinationId),
});

export const moveFiles = defineMutation({
  mutation: ({
    fileIds,
    destinationId,
  }: {
    fileIds: string[];
    destinationId: string;
  }) => fileApi.moveFiles(fileIds, destinationId),
});
