import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { documentTextStatsApi } from "@/api/documentTextStats";
import { DOCUMENT_TEXT_STATS_ROOT } from "@/queries/documentTextStats";

export const useRetryFailedDocumentText = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: () => documentTextStatsApi.retryFailed(),
    onSettled() {
      queryCache.invalidateQueries({ key: [...DOCUMENT_TEXT_STATS_ROOT] });
    },
  });
});
