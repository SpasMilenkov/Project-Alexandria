import { defineQueryOptions } from "@pinia/colada";

import { type AdminJobsQuery, jobsApi } from "@/api/jobs";

export const JOBS_QUERY_KEYS = {
  list: (query: AdminJobsQuery) => [
    ...JOBS_QUERY_KEYS.root,
    "list",
    query.type,
    query.status,
    query.page,
    query.pageSize,
  ],
  root: ["admin", "jobs"] as const,
};

export const adminJobs = defineQueryOptions((query: AdminJobsQuery) => ({
  key: JOBS_QUERY_KEYS.list(query),
  placeholderData: (prev) => prev,
  query: () => jobsApi.getJobs(query),
  staleTime: 15_000,
}));
