import { api } from "./client";

export interface OutboxDeadMessage {
  id: string;
  routingKey: string;
  attempts: number;
  lastError: string | null;
  createdAt: string;
  deadAt: string;
}

export interface OutboxSummary {
  pendingCount: number;
  oldestPendingAt: string | null;
  deadCount: number;
  recentDead: OutboxDeadMessage[];
}

export interface ResendDeadResult {
  resent: number;
}

export const outboxApi = {
  getSummary: (): Promise<OutboxSummary> => api.get<OutboxSummary>("/admin/outbox"),

  resendDead: (): Promise<ResendDeadResult> =>
    api.post<ResendDeadResult>("/admin/outbox/resend-dead"),
};
