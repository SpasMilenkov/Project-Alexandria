import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { OperationalEvent } from "@/api/monitoring";

import { isAuthError } from "@/api/shuffle";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useRollingNow } from "@/composables/useRollingNow";
import { OperationalEventSeverity, type ServiceType } from "@/enums";
import { serviceHealth } from "@/features/monitoring/display";
import { UPTIME_WINDOW_DAYS, serviceUptimes, statusHistory } from "@/queries/monitoring";
import { formatCount } from "@/utils/count";

const DAY_MS = 24 * 60 * 60 * 1000;

export type HealthLevel = "healthy" | "degraded" | "down";

export interface OverallStatus {
  color: "success" | "warning" | "error";
  label: string;
}

const LEVEL_RANK: Record<HealthLevel, number> = { degraded: 1, down: 2, healthy: 0 };

const levelOf = (event: OperationalEvent | undefined): HealthLevel => {
  if (!event) return "healthy";

  if (event.severity === OperationalEventSeverity.Failure) return "down";

  return "degraded";
};

/**
 * Live per-service health (active incident) and trailing uptime, shared by the admin
 * hubs and the health strip. Both queries poll while the tab is visible unless `poll` is off.
 */
export const useServiceStatus = ({ poll = true }: { poll?: boolean } = {}) => {
  // Five-minute steps keep the uptime key stable between polls while the window advances.
  const { now } = useRollingNow(5 * 60 * 1000);

  const uptimeRange = computed(() => ({
    from: new Date(now.value - UPTIME_WINDOW_DAYS * DAY_MS),
    to: new Date(now.value),
  }));

  const history = useQuery(statusHistory());
  const uptimes = useQuery(() => serviceUptimes(uptimeRange.value));

  // A page that also mounts the health strip reads the shared cache without polling twice.
  if (poll) {
    useLiveRefresh(history.refetch, 30_000);
    useLiveRefresh(uptimes.refetch, 60_000);
  }

  // Rows must never render from missing data: that reads as "all healthy". A suppressed
  // auth error keeps the spinner up until the refreshed session refetches.
  const isLoading = computed(() => {
    const historyMissing =
      history.data.value === undefined &&
      (history.isLoading.value || isAuthError(history.error.value));
    const uptimesMissing =
      uptimes.data.value === undefined &&
      (uptimes.isLoading.value || isAuthError(uptimes.error.value));

    return historyMissing || uptimesMissing;
  });

  const isFetching = computed(() => history.isLoading.value || uptimes.isLoading.value);

  const visibleError = computed((): Error | null => {
    const err = history.error.value ?? uptimes.error.value;

    if (!err || isAuthError(err)) return null;

    return err;
  });

  const activeByService = computed<Partial<Record<ServiceType, OperationalEvent>>>(() => {
    const map: Partial<Record<ServiceType, OperationalEvent>> = {};

    for (const event of history.data.value ?? []) {
      map[event.serviceType] = event;
    }

    return map;
  });

  const activeIncident = (service: ServiceType) => activeByService.value[service];

  const healthOf = (service: ServiceType) => serviceHealth(activeIncident(service));

  const levelOfService = (service: ServiceType) => levelOf(activeIncident(service));

  const uptimeOf = (service: ServiceType) => uptimes.data.value?.[service];

  // Several services can back one surface (previews): report the worst of them.
  const worstOf = (services: ServiceType[]) => {
    const ranked = [...services].sort(
      (a, b) => LEVEL_RANK[levelOfService(b)] - LEVEL_RANK[levelOfService(a)],
    );
    const worst = ranked[0];
    const known = services
      .map((service) => uptimeOf(service))
      .filter((value): value is number => value !== undefined);

    return {
      health: healthOf(worst),
      incident: activeIncident(worst),
      service: worst,
      uptime: known.length > 0 ? Math.min(...known) : undefined,
    };
  };

  const summarize = (services: ServiceType[]): OverallStatus => {
    const levels = services.map(levelOfService);
    const down = levels.filter((level) => level === "down").length;
    const degraded = levels.filter((level) => level === "degraded").length;

    if (down > 0 && degraded > 0) {
      return { color: "error", label: `${down} down, ${degraded} degraded` };
    }

    if (down > 0) return { color: "error", label: `${formatCount(down, "service")} down` };

    if (degraded > 0) return { color: "warning", label: `${formatCount(degraded, "service")} degraded` };

    return { color: "success", label: "All systems operational" };
  };

  const refetch = () => Promise.all([history.refetch(), uptimes.refetch()]);

  return {
    activeIncident,
    healthOf,
    isFetching,
    isLoading,
    refetch,
    summarize,
    uptimeOf,
    visibleError,
    worstOf,
  };
};

// Floor rather than round so 99.96% never displays as a perfect 100.0%.
export const formatUptime = (value: number | undefined): string => {
  if (value === undefined) return "—";

  const clamped = Math.min(Math.max(value, 0), 100);

  return `${(Math.floor(clamped * 10) / 10).toFixed(1)}%`;
};

export const uptimeClass = (value: number | undefined): string => {
  if (value === undefined) return "text-gray-600 dark:text-gray-400";

  if (value >= 99.5) return "text-emerald-600 dark:text-emerald-400";

  if (value >= 95) return "text-amber-600 dark:text-amber-400";

  return "text-red-600 dark:text-red-400";
};
