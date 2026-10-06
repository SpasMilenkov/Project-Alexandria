import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { statsApi } from "@/api/stats";
import { STATS_QUERY_KEYS } from "@/queries/stats";

export const finalizeSummary = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: ({ kind, from, to }: { kind: string; from: string; to: string }) =>
      statsApi.finalizeSummary(kind, from, to),

    onSettled() {
      queryCache.invalidateQueries({ key: STATS_QUERY_KEYS.root });
    },
  });
});
