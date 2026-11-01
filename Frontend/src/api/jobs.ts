import type { JobStatus, JobType } from "@/enums";

import type { PaginatedResponse } from "./directory";

import { apiClient } from "./client";

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
  getJobs: async (query: AdminJobsQuery): Promise<PaginatedResponse<AdminJob>> => {
    const result = await apiClient.get<PaginatedResponse<AdminJob>>("/admin/jobs", {
      params: query,
    });
    return result.data;
  },

  requeueFailed: async (type: JobType): Promise<RequeueFailedResult> => {
    const result = await apiClient.post<RequeueFailedResult>("/admin/jobs/requeue-failed", {
      type,
    });
    return result.data;
  },

  requeueJob: async (jobId: string): Promise<void> => {
    await apiClient.post(`/admin/jobs/${jobId}/requeue`);
  },
};
