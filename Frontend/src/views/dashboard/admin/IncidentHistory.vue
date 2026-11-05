<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { nextTick, ref, watch } from "vue";
import { type LocationQueryRaw, useRoute, useRouter } from "vue-router";

import type { EventCalendarRange } from "@/api/monitoring";
import type { OperationalEventSeverity, ServiceType } from "@/enums";

import ErrorCalendar from "@/components/dashboard/admin/monitoring/ErrorCalendar.vue";
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import ServiceHealthStrip from "@/components/dashboard/admin/monitoring/ServiceHealthStrip.vue";
import { dayWindowFor, parseDeepLinkQuery } from "@/features/monitoring/service-routing";

const route = useRoute();
const router = useRouter();

const feedDateRange = ref<EventCalendarRange | null>(null);
const feedSection = ref<HTMLElement | null>(null);
const refreshing = ref(false);

// Deep-link seeds (P6): applied on mount and whenever the query changes, so
// same-page producers (strip cards) and external links behave identically.
const initialServiceType = ref<ServiceType | null>(null);
const initialSeverity = ref<OperationalEventSeverity | null>(null);
const highlightId = ref<string | null>(null);

const scrollToFeed = () => {
  void nextTick(() => {
    feedSection.value?.scrollIntoView({ behavior: "smooth", block: "start" });
  });
};

const applyDeepLink = () => {
  const params = parseDeepLinkQuery(route.query);

  initialServiceType.value = params.service ?? null;
  initialSeverity.value = params.severity ?? null;
  highlightId.value = params.eventId ?? null;

  // The URL is the source of truth: a link without a window must clear the previous one.
  if (!params.from || !params.to) {
    feedDateRange.value = null;
    return;
  }

  feedDateRange.value = { from: params.from, to: params.to };
  scrollToFeed();
};

// Drops keys from the current query; "undefined" removes them from the URL.
const queryWithout = (...keys: string[]): LocationQueryRaw => {
  const next: LocationQueryRaw = { ...route.query };
  for (const key of keys) {
    next[key] = undefined;
  }
  return next;
};

watch(() => route.query, applyDeepLink, { immediate: true });

// Calendar day click → filter the log to that single day. The backend groups
// events into UTC calendar days, so bounds are built with Date.UTC from the
// cell's local Y/M/D — matching the cell exactly no matter the viewer's offset.
// The window is also synced into the URL so the view is shareable.
const handleViewDay = (date: Date) => {
  const range = dayWindowFor(date);
  feedDateRange.value = range;
  scrollToFeed();
  // A day filter replaces any incident-specific selection carried by the previous link.
  void router.replace({
    query: {
      ...queryWithout("eventId", "severity"),
      from: range.from.toISOString(),
      to: range.to.toISOString(),
    },
  });
};

const handleClearDateRange = () => {
  feedDateRange.value = null;
  void router.replace({ query: queryWithout("from", "to") });
};

// One invalidate covers calendar, uptimes, status history and the event log
const queryCache = useQueryCache();

const handleRefresh = async () => {
  refreshing.value = true;
  try {
    await queryCache.invalidateQueries({ key: ["monitoring"] });
  } finally {
    refreshing.value = false;
  }
};
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-calendar-clock"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Incidents</h1>
      </div>

      <UTooltip text="Refresh">
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          icon="i-lucide-refresh-cw"
          square
          class="justify-center"
          aria-label="Refresh incidents"
          :loading="refreshing"
          @click="handleRefresh()"
        />
      </UTooltip>
    </header>

    <ServiceHealthStrip />

    <ErrorCalendar @view-day="handleViewDay" />

    <div ref="feedSection" class="scroll-mt-3">
      <EventsFeed
        :date-range="feedDateRange"
        :initial-service-type="initialServiceType"
        :initial-severity="initialSeverity"
        :highlight-id="highlightId"
        @clear-date-range="handleClearDateRange"
      />
    </div>
  </div>
</template>
