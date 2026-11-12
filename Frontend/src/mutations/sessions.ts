import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { authApi } from "@/api/auth";
import { ACTIVITY_QUERY_KEYS } from "@/queries/activities";
import { SESSION_QUERY_KEYS } from "@/queries/sessions";

export const revokeSession = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (sessionId: string) => authApi.revokeSession(sessionId),
    onSettled() {
      queryCache.invalidateQueries({ key: SESSION_QUERY_KEYS.root });
      queryCache.invalidateQueries({ key: ACTIVITY_QUERY_KEYS.root });
    },
  });
});
