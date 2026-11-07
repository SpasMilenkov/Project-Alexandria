import type { JobStatus, JobType } from "@/enums";

import type { PaginatedResponse } from "./directory";

import { api } from "./client";

export interface AdminJob {
  id: string;
  type: JobType;
  status: JobStatus;
  retryCount: number;
  errorDetail: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  updatedAt: string | null;
}

export interface AdminJobsQuery {
  type: JobType;
  status: JobStatus;
  page: number;
  pageSize: number;
}

export interface RequeueFailedResult {
  requeued: number;
}

export const jobsApi = {
  getJobs: (query: AdminJobsQuery): Promise<PaginatedResponse<AdminJob>> =>
    api.get<PaginatedResponse<AdminJob>>("/admin/jobs", {
      params: query,
    }),

  requeueFailed: (type: JobType): Promise<RequeueFailedResult> =>
    api.post<RequeueFailedResult>("/admin/jobs/requeue-failed", {
      type,
    }),

  requeueJob: async (jobId: string): Promise<void> => {
    await api.post(`/admin/jobs/${jobId}/requeue`);
  },
};
