import { defineQueryOptions } from "@pinia/colada";

import type { ServiceType } from "@/enums";

import {
  type EventCalendarRange,
  type OperationalEventsQuery,
  monitoringApi,
} from "@/api/monitoring";

const ROOT = ["monitoring"] as const;

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
}));

// Fixed trailing window: the strip always shows the last 30 days.
export const UPTIME_WINDOW_DAYS = 30;

export const serviceUptimes = defineQueryOptions((range: EventCalendarRange) => ({
  key: [...ROOT, "uptimes", range.from.toISOString(), range.to.toISOString()],
  query: async (): Promise<Partial<Record<ServiceType, number>>> => {
    const uptimes = await monitoringApi.getAllUptimes(range);
    const byService: Partial<Record<ServiceType, number>> = {};
    for (const entry of uptimes) {
      byService[entry.service] = entry.uptime;
    }
    return byService;
  },
  placeholderData: (prev) => prev,
  staleTime: 60_000,
}));
