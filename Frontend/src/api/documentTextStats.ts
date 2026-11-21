import type { DocumentTextStatus } from "@/enums";

import { api } from "./client";

export interface DocumentTextStatusCount {
  status: DocumentTextStatus;
  count: number;
}

export interface DocumentTextFailure {
  contentObjectId: string;
  // Name, type and error are all null for another account's file: an admin never sees them.
  fileName: string | null;
  mimeType: string | null;
  errorDetail: string | null;
  attemptCount: number;
  lastAttemptAt: string | null;
  // False once the sweep has used up its attempts for this document.
  willRetry: boolean;
}

export interface DocumentTextOverviewResponse {
  statusCounts: DocumentTextStatusCount[];
  waitingCount: number;
  recentFailures: DocumentTextFailure[];
  maxAttempts: number;
}

export interface RetryFailedDocumentTextResponse {
  reset: number;
}

export const documentTextStatsApi = {
  getOverview: (): Promise<DocumentTextOverviewResponse> =>
    api.get<DocumentTextOverviewResponse>("/admin/document-text/overview"),

  retryFailed: (): Promise<RetryFailedDocumentTextResponse> =>
    api.post<RetryFailedDocumentTextResponse>("/admin/document-text/retry-failed"),
};
