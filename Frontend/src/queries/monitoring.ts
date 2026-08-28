import { defineQueryOptions } from "@pinia/colada";

import type { EventCalendarRange, OperationalEventsQuery } from "@/api/monitoring";

import { monitoringApi } from "@/api/monitoring";
import { ServiceType } from "@/enums";

const ROOT = ["monitoring"] as const;

// staleTime mirrors each endpoint's server-side ResponseCache
export const errorCalendar = defineQueryOptions((range: EventCalendarRange) => ({
  key: [...ROOT, "calendar", range.from.toISOString(), range.to.toISOString()],
  query: () => monitoringApi.getErrorCalendar(range),
  staleTime: 60_000,
}));

export const operationalEvents = defineQueryOptions((query: OperationalEventsQuery) => ({
  key: [
    ...ROOT,
    "events",
    query.serviceType ?? null,
    query.status ?? null,
    query.severity ?? null,
    query.code ?? null,
    query.from?.toISOString() ?? null,
    query.to?.toISOString() ?? null,
    query.page,
    query.pageSize,
  ],
  placeholderData: (prev) => prev,
  query: () => monitoringApi.getOperationalEvents(query),
}));

export const statusHistory = defineQueryOptions(() => ({
  key: [...ROOT, "status-history"],
  query: () => monitoringApi.getStatusHistory(),
  staleTime: 10_000,
  // Drives the "live" health cards; backend ResponseCache(10) tolerates this cadence
  refetchInterval: 30_000,
}));

// Fixed trailing window (Option A): the strip always shows the last 30 days.
export const UPTIME_WINDOW_DAYS = 30;

const ALL_SERVICES: ServiceType[] = [
  ServiceType.Api,
  ServiceType.MediaPreviews,
  ServiceType.DocumentPreviews,
  ServiceType.Transpilation,
  ServiceType.Lyrics,
  ServiceType.MediaMetadata,
];

export const serviceUptimes = defineQueryOptions((range: EventCalendarRange) => ({
  key: [...ROOT, "uptimes", range.from.toISOString(), range.to.toISOString()],
  query: async (): Promise<Record<ServiceType, number>> => {
    const entries = await Promise.all(
      ALL_SERVICES.map(
        async (service) => [service, await monitoringApi.getUptime(service, range)] as const,
      ),
    );
    return Object.fromEntries(entries) as Record<ServiceType, number>;
  },
  staleTime: 60_000,
  // Trailing-window numbers move slowly; matches the endpoint's ResponseCache(60)
  refetchInterval: 60_000,
}));
