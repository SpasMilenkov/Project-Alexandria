import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import {
  type CloseSessionRequest,
  type QueueTranspilationJobRequest,
  type StartSessionRequest,
  type UpdateTranspilationJobRequest,
  streamingApi,
} from "@/api/streaming";
import { STREAMING_QUERY_KEYS } from "@/queries/streaming";

export const queueTranspilationJob = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (params: QueueTranspilationJobRequest) => streamingApi.queueTranspilationJob(params),
    onSettled() {
      queryCache.invalidateQueries({ key: [...STREAMING_QUERY_KEYS.root, "transpilation-jobs"] });
    },
  });
});

export const startSession = defineMutation(() => {
  return useMutation({
    mutation: (req: StartSessionRequest) => streamingApi.startSession(req),
    // no cache invalidation here: the session list is not shown during active playback,
    // and the history entry update comes from closeSession
  });
});

export const closeSession = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ sessionId, req }: { sessionId: string; req: CloseSessionRequest }) =>
      streamingApi.closeSession(sessionId, req),
    onSettled(_data, _error, vars) {
      // invalidate the paginated history list
      queryCache.invalidateQueries({ key: [...STREAMING_QUERY_KEYS.root, "history"] });
      // invalidate the sessions list for the parent history row if we have it
      if (_data) {
        queryCache.invalidateQueries({
          key: STREAMING_QUERY_KEYS.sessions(_data.id),
        });
        queryCache.invalidateQueries({
          key: STREAMING_QUERY_KEYS.historyByFile(_data.fileId),
        });
      }
    },
  });
});

export const stopTranspilationJob = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (req: UpdateTranspilationJobRequest) => streamingApi.updateTranspilationJob(req),
    onSettled() {
      queryCache.invalidateQueries({ key: [...STREAMING_QUERY_KEYS.root, "transpilation-jobs"] });
    },
  });
});
