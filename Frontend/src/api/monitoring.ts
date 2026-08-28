import {
  OperationalEventCode,
  OperationalEventSeverity,
  OperationalEventStatus,
  ServiceType,
} from "@/enums";

import type { PaginatedResponse } from "./directory";

import { apiClient } from "./client";

// Request/response types

export interface ErrorAggregate {
  day: string; // "yyyy-MM-dd"
  serviceType: ServiceType;
  severity: OperationalEventSeverity;
  count: number;
}

export interface OperationalEvent {
  id: string;
  serviceType: ServiceType;
  status: OperationalEventStatus;
  code: OperationalEventCode;
  severity: OperationalEventSeverity;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string | null;
  resolvedAt: string | null;
  createdBy: string;
}

export interface EventCalendarRange {
  from: Date;
  to: Date;
}

export interface ReportProblemPayload {
  description: string;
  pageContext?: string | null;
}
export interface OperationalEventsQuery {
  serviceType?: ServiceType;
  status?: OperationalEventStatus;
  severity?: OperationalEventSeverity;
  code?: OperationalEventCode;
  from?: Date;
  to?: Date;
  page: number;
  pageSize: number;
}

// Metadata is stored as a JSON string on the wire, discriminated by "kind".
// resolutionNote is appended server-side when an admin resolves the incident.
export type EventMetadata =
  | {
      kind: "healthcheck-failure";
      serviceInstance: string;
      checkName: string;
      detail?: string | null;
      resolutionNote?: string | null;
    }
  | {
      kind: "error-rate-threshold";
      serviceInstance: string;
      windowMinutes: number;
      failureCount: number;
      threshold: number;
      resolutionNote?: string | null;
    }
  | {
      kind: "user-reported";
      serviceInstance: string;
      description: string;
      pageContext?: string | null;
      resolutionNote?: string | null;
    };

// Metadata rows written before the backend serializer fix are PascalCase and
// carry no "kind" discriminator — fold both shapes onto camelCase keys first.
const toCamelCaseRecord = (raw: Record<string, unknown>): Record<string, unknown> => ({
  kind: raw.kind,
  serviceInstance: raw.serviceInstance ?? raw.ServiceInstance,
  checkName: raw.checkName ?? raw.CheckName,
  detail: raw.detail ?? raw.Detail,
  windowMinutes: raw.windowMinutes ?? raw.WindowMinutes,
  failureCount: raw.failureCount ?? raw.FailureCount,
  threshold: raw.threshold ?? raw.Threshold,
  description: raw.description ?? raw.Description,
  pageContext: raw.pageContext ?? raw.PageContext,
  resolutionNote: raw.resolutionNote ?? raw.ResolutionNote,
});

const buildMetadata = (m: Record<string, unknown>): EventMetadata | null => {
  const str = (v: unknown): string | undefined =>
    v === undefined || v === null || v === "" ? undefined : String(v);
  const num = (v: unknown): number => Number(v ?? 0);

  const kind = typeof m.kind === "string" ? m.kind : undefined;
  const serviceInstance = str(m.serviceInstance) ?? "unknown";
  const resolutionNote = str(m.resolutionNote) ?? null;

  if (kind === "healthcheck-failure" || (!kind && str(m.checkName) !== undefined)) {
    const checkName = str(m.checkName);
    if (!checkName) return null;
    return {
      kind: "healthcheck-failure",
      serviceInstance,
      checkName,
      detail: str(m.detail) ?? null,
      resolutionNote,
    };
  }

  if (kind === "error-rate-threshold" || (!kind && m.windowMinutes != null)) {
    return {
      kind: "error-rate-threshold",
      serviceInstance,
      windowMinutes: num(m.windowMinutes),
      failureCount: num(m.failureCount),
      threshold: num(m.threshold),
      resolutionNote,
    };
  }

  const description = str(m.description);
  if (kind === "user-reported" || (!kind && description !== undefined)) {
    if (!description) return null;
    return {
      kind: "user-reported",
      serviceInstance,
      description,
      pageContext: str(m.pageContext) ?? null,
      resolutionNote,
    };
  }

  return null;
};

export const parseEventMetadata = (raw: string | null): EventMetadata | null => {
  if (!raw) return null;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object") return null;
    return buildMetadata(toCamelCaseRecord(parsed as Record<string, unknown>));
  } catch {
    return null;
  }
};

export const monitoringApi = {
  getErrorCalendar: async (range: EventCalendarRange): Promise<ErrorAggregate[]> => {
    const result = await apiClient.get<ErrorAggregate[]>("/monitoring/events/calendar", {
      params: { from: range.from.toISOString(), to: range.to.toISOString() },
    });
    return result.data;
  },

  getOperationalEvents: async (
    query: OperationalEventsQuery,
  ): Promise<PaginatedResponse<OperationalEvent>> => {
    const result = await apiClient.get<PaginatedResponse<OperationalEvent>>("/monitoring/events", {
      params: {
        code: query.code,
        from: query.from?.toISOString(),
        page: query.page,
        pageSize: query.pageSize,
        severity: query.severity,
        serviceType: query.serviceType,
        status: query.status,
        to: query.to?.toISOString(),
      },
    });
    return result.data;
  },

  getStatusHistory: async (): Promise<OperationalEvent[]> => {
    const result = await apiClient.get<OperationalEvent[]>("/monitoring/status/history");
    return result.data;
  },

  getUptime: async (service: ServiceType, range: EventCalendarRange): Promise<number> => {
    const result = await apiClient.get<number>("/monitoring/uptime", {
      params: { service, from: range.from.toISOString(), to: range.to.toISOString() },
    });
    return result.data;
  },

  reportProblem: async (payload: ReportProblemPayload): Promise<OperationalEvent> => {
    const result = await apiClient.post<OperationalEvent>("/monitoring/report", payload);
    return result.data;
  },

  resolveIncident: async (id: string, note?: string): Promise<OperationalEvent> => {
    const result = await apiClient.patch<OperationalEvent>(`/monitoring/events/${id}/resolve`, {
      note: note?.trim() ? note.trim() : undefined,
    });
    return result.data;
  },
};
