import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { statusApi } from "@/api/status";
import { STATUS_QUERY_KEYS } from "@/queries/status";

export const useDeletePreview = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (previewId: string) => statusApi.deletePreview(previewId),
    onSettled() {
      queryCache.invalidateQueries({ key: STATUS_QUERY_KEYS.root });
    },
  });
});

export const useDeletePreviewsByFile = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileId, createdBefore }: { fileId: string; createdBefore?: string }) =>
      statusApi.deletePreviewsByFile(fileId, createdBefore),
    onSettled() {
      queryCache.invalidateQueries({ key: STATUS_QUERY_KEYS.root });
    },
  });
});
