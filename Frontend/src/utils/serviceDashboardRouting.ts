import type { LocationQuery } from "vue-router";

import type { EventCalendarRange, OperationalEvent } from "@/api/monitoring";

import { OperationalEventSeverity, ServiceType } from "@/enums";

// Services without a dedicated dashboard degrade to the generic filtered
// timeline — never a dead link (D12).
export const FALLBACK_ROUTE = "/dashboard/admin/incident-history";

export const SERVICE_DASHBOARD_ROUTES: Partial<Record<ServiceType, string>> = {
  [ServiceType.MediaMetadata]: "/dashboard/admin/integrations/audio-analysis",
  [ServiceType.Transpilation]: "/dashboard/admin/integrations/transpilation",
  [ServiceType.MediaPreviews]: "/dashboard/admin/integrations/previews",
  [ServiceType.DocumentPreviews]: "/dashboard/admin/integrations/previews",
  [ServiceType.Lyrics]: "/dashboard/admin/integrations/lyrics",
};

// What producers emit: everything already URL-string shaped.
export interface DeepLinkParams {
  service?: ServiceType;
  from?: string;
  to?: string;
  severity?: OperationalEventSeverity;
  eventId?: string;
}

// What consumers receive after validation: dates parsed back into Date objects.
export interface ParsedDeepLink {
  service?: ServiceType;
  from?: Date;
  to?: Date;
  severity?: OperationalEventSeverity;
  eventId?: string;
}

export const buildDeepLink = (event: OperationalEvent): Record<string, string> => ({
  from: event.createdAt,
  to: event.resolvedAt ?? new Date().toISOString(),
  severity: String(event.severity),
  eventId: event.id,
});

const parseEnumValue = <T extends number>(raw: unknown, maxExclusive: number): T | undefined => {
  if (typeof raw !== "string" || !/^\d+$/.test(raw)) return undefined;
  const value = Number(raw);
  if (value < 0 || value >= maxExclusive) return undefined;
  return value as T;
};

const parseIsoDate = (raw: unknown): Date | undefined => {
  if (typeof raw !== "string") return undefined;
  const date = new Date(raw);
  return Number.isNaN(date.getTime()) ? undefined : date;
};

const firstQueryValue = (value: LocationQuery[string]): string | undefined =>
  Array.isArray(value) ? (value[0] as string | undefined) : (value as string | undefined);

// Inverse of the producers: turns route query into typed params. Invalid or
// partial windows are dropped rather than guessed at.
export const parseDeepLinkQuery = (query: LocationQuery): ParsedDeepLink => {
  const params: ParsedDeepLink = {};

  const serviceRaw = firstQueryValue(query.service);
  const service = parseEnumValue<ServiceType>(serviceRaw, 6);
  if (service !== undefined) params.service = service;

  const severityRaw = firstQueryValue(query.severity);
  const severity = parseEnumValue<OperationalEventSeverity>(severityRaw, 3);
  if (severity !== undefined) params.severity = severity;

  const eventId = firstQueryValue(query.eventId);
  if (eventId) params.eventId = eventId;

  const from = parseIsoDate(firstQueryValue(query.from));
  const to = parseIsoDate(firstQueryValue(query.to));
  if (from && to && from < to) {
    params.from = from;
    params.to = to;
  }

  return params;
};

// Convenience for the most common producer: a time window covering one UTC day.
export const dayWindowFor = (date: Date): EventCalendarRange => {
  const from = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  const to = new Date(from);
  to.setUTCDate(to.getUTCDate() + 1);
  return { from, to };
};
