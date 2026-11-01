import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type { JobType } from "@/enums";

import { jobsApi } from "@/api/jobs";
import { JOBS_QUERY_KEYS } from "@/queries/jobs";

// Requeues move jobs between status counts, so the per-service stats refresh too.
const STATS_KEYS = [
  ["previews-stats"],
  ["transpilation-stats"],
  ["lyrics-stats"],
  ["audio-analysis"],
] as const;

const invalidateJobViews = (queryCache: ReturnType<typeof useQueryCache>) => {
  queryCache.invalidateQueries({ key: [...JOBS_QUERY_KEYS.root] });
  for (const key of STATS_KEYS) {
    queryCache.invalidateQueries({ key: [...key] });
  }
};

export const useRequeueJob = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (jobId: string) => jobsApi.requeueJob(jobId),
    onSettled() {
      invalidateJobViews(queryCache);
    },
  });
});

export const useRequeueFailedJobs = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (type: JobType) => jobsApi.requeueFailed(type),
    onSettled() {
      invalidateJobViews(queryCache);
    },
  });
});
