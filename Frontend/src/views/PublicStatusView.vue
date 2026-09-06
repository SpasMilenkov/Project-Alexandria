<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { computed } from "vue";

import type { DailyServiceStatus, PublicServiceState } from "@/api/publicStatus";
import type { ServiceType } from "@/enums";

import { publicCurrentStatus, publicStatusHistory } from "@/queries/publicStatus";
import {
  overallPublicState,
  PUBLIC_SERVICE_LABELS,
  PUBLIC_STATE_BADGE_COLORS,
  PUBLIC_STATE_BAR_COLORS,
  PUBLIC_STATE_LABELS,
} from "@/utils/publicStatus.utils";

const DAYS = 90;
const MOBILE_DAYS = 30;

// Below sm (640px), 90 daily bars get too thin to read as color — show a
// shorter trailing window instead of forcing horizontal scroll.
const isMobile = useMediaQuery("(max-width: 639px)");
const visibleWindow = computed(() => (isMobile.value ? MOBILE_DAYS : DAYS));

const {
  data: current,
  isLoading: currentLoading,
  error: currentError,
} = useQuery(publicCurrentStatus());
const {
  data: history,
  isLoading: historyLoading,
  error: historyError,
} = useQuery(publicStatusHistory(DAYS));

const isLoading = computed(() => currentLoading.value || historyLoading.value);

// Auth refresh cycles produce transient 401/403s — never surface those
const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

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
}

const currentStateOf = (service: DailyServiceStatus): PublicServiceState => {
  const live = current.value?.find((entry) => entry.serviceType === service.serviceType);
  if (live) return live.status;
  return service.days[service.days.length - 1]?.status ?? "Healthy";
};

const rows = computed<StatusRow[]>(() =>
  (history.value ?? []).map((service) => ({
    days: service.days,
    label: PUBLIC_SERVICE_LABELS[service.serviceType] ?? String(service.serviceType),
    service: service.serviceType,
    state: currentStateOf(service),
  })),
);

// Slice to the visible trailing window per breakpoint, rather than
// rendering the full 90 days and relying on scroll to reach them.
const visibleDays = (row: StatusRow) => row.days.slice(-visibleWindow.value);

const overall = computed(() => overallPublicState(rows.value.map((row) => row.state)));

const BANNER_STYLES: Record<string, { bg: string; icon: string; ring: string; text: string }> = {
  error: {
    bg: "bg-red-500/10",
    icon: "text-red-600 dark:text-red-400",
    ring: "ring-red-400/60",
    text: "text-red-600 dark:text-red-400",
  },
  success: {
    bg: "bg-emerald-500/10",
    icon: "text-emerald-600 dark:text-emerald-400",
    ring: "ring-emerald-400/60",
    text: "text-emerald-600 dark:text-emerald-400",
  },
  warning: {
    bg: "bg-amber-500/10",
    icon: "text-amber-600 dark:text-amber-400",
    ring: "ring-amber-400/60",
    text: "text-amber-600 dark:text-amber-400",
  },
};

const bannerStyle = computed(() => BANNER_STYLES[overall.value.color]);
</script>

<template>
  <div class="p-3 sm:p-6">
    <div class="max-w-3xl mx-auto space-y-4 lg:space-y-6">
      <!-- Header -->
      <header>
        <h1 class="text-xl font-semibold text-gray-900 dark:text-gray-100">Alexandria Status</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Service health and the last {{ visibleWindow }} days
        </p>
      </header>

      <!-- Loading -->
      <div v-if="isLoading" class="flex items-center justify-center py-24">
        <UIcon name="i-mdi-loading" class="w-6 h-6 text-gray-400 dark:text-gray-500 animate-spin" />
      </div>

      <UAlert
        v-else-if="visibleError"
        color="error"
        variant="subtle"
        icon="i-lucide-alert-circle"
        title="Could not load status"
        :description="visibleError.message"
      />

      <template v-else>
        <!-- Overall banner -->
        <div
          class="rounded-2xl border border-transparent px-5 py-4 flex items-center gap-3 frosted-glass ring-1"
          :class="[bannerStyle.bg, bannerStyle.ring]"
        >
          <UIcon :name="overall.icon" class="w-6 h-6 shrink-0" :class="bannerStyle.icon" />
          <p class="text-base font-semibold" :class="bannerStyle.text">{{ overall.label }}</p>
        </div>

        <!-- Per-service rows -->
        <div
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface divide-y divide-gray-200/60 dark:divide-gray-700/60 overflow-hidden"
        >
          <div v-for="row in rows" :key="row.service" class="px-5 py-4 space-y-2">
            <div class="flex items-center justify-between gap-3">
              <p class="text-sm font-medium text-gray-800 dark:text-gray-100">{{ row.label }}</p>
              <UBadge
                :color="PUBLIC_STATE_BADGE_COLORS[row.state]"
                variant="subtle"
                size="sm"
                class="shrink-0"
              >
                {{ PUBLIC_STATE_LABELS[row.state] }}
              </UBadge>
            </div>

            <!-- Bars fill the available width at every breakpoint — no
                 scroll container, no fixed min-width. -->
            <div class="flex gap-[2px] w-full">
              <div
                v-for="day in visibleDays(row)"
                :key="day.date"
                class="flex-1 h-6 rounded-[2px] min-w-[2px]"
                :class="PUBLIC_STATE_BAR_COLORS[day.status]"
                :title="`${day.date}: ${PUBLIC_STATE_LABELS[day.status]}`"
              />
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>
