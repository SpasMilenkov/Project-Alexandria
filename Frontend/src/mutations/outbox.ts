import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { outboxApi } from "@/api/outbox";
import { OUTBOX_QUERY_KEYS } from "@/queries/outbox";

export const useResendDeadOutboxMessages = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: () => outboxApi.resendDead(),
    onSettled() {
      queryCache.invalidateQueries({ key: [...OUTBOX_QUERY_KEYS.root] });
    },
  });
});
