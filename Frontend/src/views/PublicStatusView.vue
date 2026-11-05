<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { DailyServiceStatus, PublicServiceState } from "@/api/publicStatus";
import type { ServiceType } from "@/enums";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import {
  PUBLIC_SERVICE_LABELS,
  PUBLIC_STATE_BAR_COLORS,
  PUBLIC_STATE_LABELS,
  type ServiceWindowStats,
  describeDaysSince,
  describeLastIssue,
  fleetWindowStats,
  formatOperationalPercent,
  formatStatusDay,
  overallPublicState,
  serviceWindowStats,
} from "@/features/monitoring/public-status";
import { publicCurrentStatus, publicStatusHistory } from "@/queries/publicStatus";

const DAYS = 90;
const MOBILE_DAYS = 30;

const LEGEND: PublicServiceState[] = ["Healthy", "Degraded", "Down"];

// Status colour lives in the icon chip and dots, never in body text, so every
// line keeps solid contrast over a photo background.
const TONES: Record<string, { chip: string; icon: string }> = {
  error: { chip: "bg-red-500/15", icon: "text-red-600 dark:text-red-400" },
  success: { chip: "bg-emerald-500/15", icon: "text-emerald-600 dark:text-emerald-400" },
  warning: { chip: "bg-amber-500/15", icon: "text-amber-600 dark:text-amber-400" },
};

// Name, bars, status on wide screens; bars drop to their own line below lg.
const rowGridClass = "lg:grid-cols-[15rem_minmax(0,1fr)_7rem]";

const surfaceClass =
  "rounded-xl border border-gray-200/70 frosted-glass glass-surface-strong dark:border-gray-700/70";

// Below sm (640px), 90 daily bars get too thin to read as color, so show a
// shorter trailing window instead of forcing horizontal scroll.
const isMobile = useMediaQuery("(max-width: 639px)");

const visibleWindow = computed(() => {
  if (isMobile.value) {
    return MOBILE_DAYS;
  }
  return DAYS;
});

const {
  data: current,
  isPending: currentPending,
  isLoading: currentRefreshing,
  error: currentError,
  refetch: refetchCurrent,
} = useQuery(publicCurrentStatus());

const {
  data: history,
  isPending: historyPending,
  isLoading: historyRefreshing,
  error: historyError,
  refetch: refetchHistory,
} = useQuery(publicStatusHistory(DAYS));

useLiveRefresh(refetchCurrent, 60_000);

const lastUpdated = ref<Date>();

watch(current, (value) => {
  if (value) {
    lastUpdated.value = new Date();
  }
});

const timeFormatter = new Intl.DateTimeFormat(undefined, { hour: "2-digit", minute: "2-digit" });

const lastUpdatedLabel = computed(() => {
  if (!lastUpdated.value) {
    return "";
  }
  return `Updated ${timeFormatter.format(lastUpdated.value)}`;
});

const isRefreshing = computed(() => currentRefreshing.value || historyRefreshing.value);

const refreshAll = () => {
  void refetchCurrent();
  void refetchHistory();
};

// Auth refresh cycles produce transient 401/403s, never surface those
const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

// Missing data must never render as "operational": background refetches keep the
// current rows, and a suppressed auth error keeps the spinner.
const isLoading = computed(() => {
  const currentMissing =
    current.value === undefined && (currentPending.value || isAuthError(currentError.value));
  const historyMissing =
    history.value === undefined && (historyPending.value || isAuthError(historyError.value));
  return currentMissing || historyMissing;
});

const visibleError = computed(() => {
  const err = currentError.value ?? historyError.value;
  if (!err || isAuthError(err)) return null;
  return err;
});

interface StatusRow {
  label: string;
  service: ServiceType;
  state: PublicServiceState;
  days: DailyServiceStatus["days"];
  stats: ServiceWindowStats;
}

const currentStateOf = (service: DailyServiceStatus): PublicServiceState => {
  const live = current.value?.find((entry) => entry.serviceType === service.serviceType);
  if (live) return live.status;
  return service.days[service.days.length - 1]?.status ?? "Healthy";
};

const rows = computed<StatusRow[]>(() =>
  (history.value ?? []).map((service) => {
    const days = service.days.slice(-visibleWindow.value);
    return {
      days,
      label: PUBLIC_SERVICE_LABELS[service.serviceType] ?? String(service.serviceType),
      service: service.serviceType,
      state: currentStateOf(service),
      stats: serviceWindowStats(days),
    };
  }),
);

const overall = computed(() => overallPublicState(rows.value.map((row) => row.state)));
const overallTone = computed(() => TONES[overall.value.color]);

const operationalNow = computed(() => rows.value.filter((row) => row.state === "Healthy").length);

const fleet = computed(() => fleetWindowStats(rows.value.map((row) => row.days)));

const summaryTiles = computed(() => [
  {
    hint: "services running normally",
    label: "Operational now",
    value: `${operationalNow.value} / ${rows.value.length}`,
  },
  {
    hint: "with no issues on any service",
    label: "Clean days",
    value: `${fleet.value.cleanDays} / ${fleet.value.totalDays}`,
  },
  {
    hint: `in the last ${visibleWindow.value} days`,
    label: "Last disruption",
    value: describeDaysSince(fleet.value.daysSinceDisruption),
  },
]);

const rowMeta = (row: StatusRow) =>
  `${formatOperationalPercent(row.stats.operationalPercent)} operational · ${describeLastIssue(row.stats.daysSinceIssue, row.days.length)}`;

const barsLabel = (row: StatusRow) =>
  `${row.label}, last ${row.days.length} days: ${row.days.length - row.stats.issueDays} operational, ${row.stats.issueDays} with issues`;

const dayTitle = (day: DailyServiceStatus["days"][number]) =>
  `${formatStatusDay(day.date)}: ${PUBLIC_STATE_LABELS[day.status]}`;
</script>

<template>
  <main class="flex w-full flex-col gap-3 self-start p-3" aria-labelledby="status-heading">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 px-4 py-4 md:px-5"
      :class="surfaceClass"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-activity" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />

        <div class="min-w-0">
          <h1
            id="status-heading"
            class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100"
          >
            Alexandria Status
          </h1>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Live service health and the last {{ visibleWindow }} days
          </p>
        </div>
      </div>

      <div
        v-if="!isLoading"
        class="flex items-center gap-3 max-sm:w-full max-sm:justify-between max-sm:border-t max-sm:border-gray-200/70 max-sm:pt-3 max-sm:dark:border-gray-700/70"
      >
        <span class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
          {{ lastUpdatedLabel }}
        </span>

        <UButton
          icon="i-lucide-refresh-cw"
          label="Refresh"
          color="neutral"
          variant="outline"
          size="sm"
          class="justify-center"
          :loading="isRefreshing"
          @click="refreshAll"
        />
      </div>
    </header>

    <div v-if="isLoading" class="flex items-center justify-center py-24" :class="surfaceClass">
      <BlockSpinner :size="32" class="text-gray-500 dark:text-gray-400" />
    </div>

    <div
      v-else-if="visibleError"
      role="alert"
      class="flex flex-col items-center justify-center gap-4 px-6 py-12 text-center"
      :class="surfaceClass"
    >
      <UIcon name="i-lucide-alert-circle" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

      <div class="space-y-1">
        <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
          Could not load status
        </h2>
        <p class="text-sm text-gray-600 dark:text-gray-400">{{ visibleError.message }}</p>
      </div>

      <UButton color="neutral" variant="outline" label="Try again" @click="refreshAll" />
    </div>

    <template v-else>
      <section aria-label="Overall status" class="shrink-0 lg:flex" :class="surfaceClass">
        <div class="flex items-center gap-4 px-4 py-4 md:px-5 lg:flex-1">
          <span
            class="flex size-11 shrink-0 items-center justify-center rounded-xl"
            :class="overallTone.chip"
          >
            <UIcon :name="overall.icon" class="size-6" :class="overallTone.icon" />
          </span>

          <div class="min-w-0">
            <p class="text-lg font-semibold text-gray-900 dark:text-gray-100" role="status">
              {{ overall.label }}
            </p>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              {{ operationalNow }} of {{ rows.length }} services operational right now
            </p>
          </div>
        </div>

        <dl
          class="grid grid-cols-1 divide-y divide-gray-200/70 border-t border-gray-200/70 min-[400px]:grid-cols-3 min-[400px]:divide-x min-[400px]:divide-y-0 lg:w-[40rem] lg:shrink-0 lg:border-t-0 lg:border-l dark:divide-gray-700/70 dark:border-gray-700/70"
        >
          <div
            v-for="tile in summaryTiles"
            :key="tile.label"
            class="grid min-w-0 grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 px-4 py-3 min-[400px]:flex min-[400px]:flex-col min-[400px]:items-start min-[400px]:justify-center min-[400px]:px-3 sm:px-4 md:px-5"
          >
            <dt
              class="col-start-1 row-start-1 text-xs font-medium text-gray-600 dark:text-gray-400"
            >
              {{ tile.label }}
            </dt>
            <dd
              class="col-start-2 row-span-2 row-start-1 text-lg font-semibold text-gray-900 tabular-nums min-[400px]:mt-1 sm:text-xl dark:text-gray-100"
            >
              {{ tile.value }}
            </dd>
            <dd class="col-start-1 row-start-2 text-xs text-gray-600 dark:text-gray-400">
              {{ tile.hint }}
            </dd>
          </div>
        </dl>
      </section>

      <section aria-labelledby="services-heading" :class="surfaceClass">
        <div
          class="flex flex-wrap items-center justify-between gap-3 border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70"
        >
          <h2 id="services-heading" class="text-sm font-semibold text-gray-900 dark:text-gray-100">
            Services
          </h2>

          <ul class="flex flex-wrap items-center gap-x-4 gap-y-1" aria-label="Legend">
            <li
              v-for="state in LEGEND"
              :key="state"
              class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
            >
              <span class="size-2.5 rounded-sm" :class="PUBLIC_STATE_BAR_COLORS[state]" />
              {{ PUBLIC_STATE_LABELS[state] }}
            </li>
          </ul>
        </div>

        <!-- One shared column template keeps every row's bars, and the date
             axis below them, aligned on wide screens. -->
        <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
          <li
            v-for="row in rows"
            :key="row.service"
            class="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-3 px-4 py-4 md:px-5 lg:gap-x-6"
            :class="rowGridClass"
          >
            <div class="order-1 min-w-0">
              <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                {{ row.label }}
              </p>
              <p class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
                {{ rowMeta(row) }}
              </p>
            </div>

            <span
              class="order-2 flex shrink-0 items-center gap-1.5 justify-self-end text-sm font-medium text-gray-900 lg:order-3 dark:text-gray-100"
            >
              <span
                class="size-2 rounded-full"
                :class="PUBLIC_STATE_BAR_COLORS[row.state]"
                aria-hidden="true"
              />
              {{ PUBLIC_STATE_LABELS[row.state] }}
            </span>

            <!-- Bars fill the available width at every breakpoint, with no
                 scroll container and no fixed min-width. -->
            <div
              class="order-3 col-span-2 flex w-full gap-[2px] lg:order-2 lg:col-span-1"
              role="img"
              :aria-label="barsLabel(row)"
            >
              <div
                v-for="day in row.days"
                :key="day.date"
                class="h-7 min-w-[2px] flex-1 rounded-[2px]"
                :class="PUBLIC_STATE_BAR_COLORS[day.status]"
                :title="dayTitle(day)"
              />
            </div>
          </li>
        </ul>

        <div
          class="grid grid-cols-1 border-t border-gray-200/70 px-4 py-2 text-xs text-gray-600 md:px-5 lg:gap-x-6 dark:border-gray-700/70 dark:text-gray-400"
          :class="rowGridClass"
          aria-hidden="true"
        >
          <span class="hidden lg:block" />
          <span class="flex justify-between">
            <span>{{ visibleWindow }} days ago</span>
            <span>Today</span>
          </span>
        </div>
      </section>
    </template>
  </main>
</template>
