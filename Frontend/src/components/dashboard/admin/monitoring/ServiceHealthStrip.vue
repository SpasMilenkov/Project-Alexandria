<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { OperationalEvent } from "@/api/monitoring";

import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useRollingNow } from "@/composables/useRollingNow";
import { useServiceDashboardRouting } from "@/composables/useServiceDashboardRouting";
import { OperationalEventSeverity, ServiceType } from "@/enums";
import { UPTIME_WINDOW_DAYS, serviceUptimes, statusHistory } from "@/queries/monitoring";
import { formatRelativeShort } from "@/utils/date-formatters";
import {
  MONITORED_SERVICES,
  SERVICE_ICONS,
  SERVICE_LABELS,
  serviceHealth,
} from "@/utils/monitoring-display.utils";
import { buildDeepLink } from "@/utils/serviceDashboardRouting";

const SERVICES = MONITORED_SERVICES;
const DAY_MS = 24 * 60 * 60 * 1000;

// Five-minute steps keep the uptime key stable between polls while the window still advances.
const { now } = useRollingNow(5 * 60 * 1000);
const uptimeRange = computed(() => ({
  from: new Date(now.value - UPTIME_WINDOW_DAYS * DAY_MS),
  to: new Date(now.value),
}));

const {
  data: history,
  isLoading: historyFetching,
  error: historyError,
  refetch: refetchHistory,
} = useQuery(statusHistory());
const {
  data: uptimes,
  isLoading: uptimesFetching,
  error: uptimesError,
  refetch: refetchUptimes,
} = useQuery(() => serviceUptimes(uptimeRange.value));

useLiveRefresh(refetchHistory, 30_000);
useLiveRefresh(refetchUptimes, 60_000);

// Auth refresh cycles produce transient 401/403s — never surface those
const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

// Rows must never render from missing data: that reads as "all healthy". Background
// refetches keep the current rows; a suppressed auth error keeps the spinner.
const isLoading = computed(() => {
  const historyMissing =
    history.value === undefined && (historyFetching.value || isAuthError(historyError.value));
  const uptimesMissing =
    uptimes.value === undefined && (uptimesFetching.value || isAuthError(uptimesError.value));
  return historyMissing || uptimesMissing;
});

// A silent failure would render fake "all healthy" rows — surface it instead
const visibleError = computed((): Error | null => {
  const err = historyError.value ?? uptimesError.value;
  if (!err || isAuthError(err)) return null;
  return err;
});

const activeByService = computed<Partial<Record<ServiceType, OperationalEvent>>>(() => {
  const map: Partial<Record<ServiceType, OperationalEvent>> = {};
  for (const event of history.value ?? []) {
    map[event.serviceType] = event;
  }
  return map;
});

const rows = computed(() =>
  SERVICES.map((service) => {
    const active = activeByService.value[service];
    const health = serviceHealth(active);
    const uptime = uptimes.value?.[service];
    // Incident age only makes sense while a service is actually in a bad state
    const age = active && health.label !== "Healthy" ? formatRelativeShort(active.createdAt) : null;
    return {
      age,
      health,
      icon: SERVICE_ICONS[service],
      label: SERVICE_LABELS[service],
      severity: active?.severity,
      service,
      uptime,
    };
  }),
);

// Single at-a-glance summary for the header — the thing you actually want to
// know before reading all six cells individually.
const overallStatus = computed(() => {
  const severities = rows.value.map((row) => row.severity).filter((s) => s !== undefined);
  const failing = severities.filter((s) => s === OperationalEventSeverity.Failure).length;
  const degraded = severities.length - failing;

  if (failing > 0) {
    return {
      color: "error" as const,
      label: `${failing} service${failing === 1 ? "" : "s"} down`,
    };
  }
  if (degraded > 0) {
    return {
      color: "warning" as const,
      label: `${degraded} service${degraded === 1 ? "" : "s"} degraded`,
    };
  }
  return { color: "success" as const, label: "All systems operational" };
});

// Floor rather than round so 99.96% never displays as a perfect 100.0%.
const formatUptime = (value: number): string => {
  const clamped = Math.min(Math.max(value, 0), 100);
  return `${(Math.floor(clamped * 10) / 10).toFixed(1)}%`;
};

const uptimeClass = (value: number | undefined): string => {
  if (value === undefined) return "text-gray-500 dark:text-gray-400";
  if (value >= 99.5) return "text-emerald-600 dark:text-emerald-400";
  if (value >= 95) return "text-amber-600 dark:text-amber-400";
  return "text-red-600 dark:text-red-400";
};

// D12 producer: a card with an active incident deep-links straight to it;
// healthy cards open the service dashboard (or timeline fallback) bare.
const { goToServiceDetail } = useServiceDashboardRouting();

type HealthRow = (typeof rows.value)[number];

const openService = (row: HealthRow) => {
  const active = activeByService.value[row.service];
  goToServiceDetail(row.service, active ? buildDeepLink(active) : {});
};
</script>

<template>
  <div
    class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
  >
    <div
      class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2 flex-wrap"
    >
      <UIcon name="i-mdi-heart-pulse" class="w-4 h-4 text-gray-400 dark:text-gray-500 shrink-0" />
      <span class="text-sm font-semibold text-gray-700 dark:text-gray-300">Service Health</span>

      <UBadge v-if="!isLoading" :color="overallStatus.color" variant="subtle" size="sm">
        {{ overallStatus.label }}
      </UBadge>

      <span class="ml-auto text-xs text-gray-500 dark:text-gray-400">
        Last {{ UPTIME_WINDOW_DAYS }} days
      </span>
    </div>

    <!-- Loading: centered spinner, consistent with the other admin sections -->
    <div v-if="isLoading" class="flex items-center justify-center py-14">
      <UIcon name="i-mdi-loading" class="w-6 h-6 text-gray-400 dark:text-gray-500 animate-spin" />
    </div>

    <UAlert
      v-else-if="visibleError"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Failed to load service health"
      :description="visibleError.message"
      class="m-4"
    />

    <template v-else>
      <!-- Mobile: list rows — icon+label left, uptime+status right, per the
           standard activity-row layout used across the app. -->
      <div class="sm:hidden divide-y divide-gray-100/50 dark:divide-gray-800/50">
        <div
          v-for="row in rows"
          :key="row.service"
          role="button"
          tabindex="0"
          class="flex items-center gap-3 px-5 py-3 cursor-pointer transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60"
          :aria-label="`Open ${row.label} details`"
          @click="openService(row)"
          @keydown.enter.self.prevent="openService(row)"
          @keydown.space.self.prevent="openService(row)"
        >
          <span class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 bg-gray-500/10">
            <UIcon :name="row.icon" class="w-4 h-4 text-gray-500 dark:text-gray-400" />
          </span>

          <div class="min-w-0 flex-1 flex items-center gap-2">
            <span class="w-2 h-2 rounded-full shrink-0" :class="row.health.dot" />
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 truncate">
              {{ row.label }}
            </p>
          </div>

          <div class="text-right shrink-0">
            <p
              class="text-sm font-bold tabular-nums leading-none"
              :class="uptimeClass(row.uptime)"
            >
              {{ row.uptime === undefined ? "—" : formatUptime(row.uptime) }}
            </p>
            <p class="text-[11px] mt-0.5" :class="row.health.text">{{ row.health.label }}</p>
            <p v-if="row.age" class="text-[10px] mt-0.5 text-gray-500 dark:text-gray-500">
              for {{ row.age }}
            </p>
          </div>
        </div>
      </div>

      <!-- Desktop: grid -->
      <div
        class="hidden sm:grid grid-cols-3 lg:grid-cols-6 divide-x divide-gray-200/60 dark:divide-gray-700/60"
      >
        <div
          v-for="row in rows"
          :key="row.service"
          role="button"
          tabindex="0"
          class="px-4 py-4 flex flex-col gap-2 cursor-pointer transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60"
          :aria-label="`Open ${row.label} details`"
          @click="openService(row)"
          @keydown.enter.self.prevent="openService(row)"
          @keydown.space.self.prevent="openService(row)"
        >
          <div class="flex items-center justify-between gap-2">
            <UIcon :name="row.icon" class="w-4 h-4 text-gray-400 dark:text-gray-500 shrink-0" />
            <span class="w-2 h-2 rounded-full shrink-0" :class="row.health.dot" />
          </div>
          <p class="text-xs font-medium text-gray-700 dark:text-gray-300 truncate">
            {{ row.label }}
          </p>
          <div>
            <p
              class="text-lg font-bold tabular-nums leading-none"
              :class="uptimeClass(row.uptime)"
            >
              {{ row.uptime === undefined ? "—" : formatUptime(row.uptime) }}
            </p>
            <p class="text-[11px] mt-1" :class="row.health.text">{{ row.health.label }}</p>
            <p v-if="row.age" class="text-[10px] mt-0.5 text-gray-500 dark:text-gray-500">
              for {{ row.age }}
            </p>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
