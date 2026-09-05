<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { nextTick, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { EventCalendarRange } from "@/api/monitoring";
import type { OperationalEventSeverity, ServiceType } from "@/enums";

import ErrorCalendar from "@/components/dashboard/admin/monitoring/ErrorCalendar.vue";
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import ServiceHealthStrip from "@/components/dashboard/admin/monitoring/ServiceHealthStrip.vue";
import { dayWindowFor, parseDeepLinkQuery } from "@/utils/serviceDashboardRouting";

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

  if (params.from && params.to) {
    feedDateRange.value = { from: params.from, to: params.to };
    scrollToFeed();
  }
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
  void router.replace({
    query: { ...route.query, from: range.from.toISOString(), to: range.to.toISOString() },
  });
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
  <div class="p-3 sm:p-5 lg:p-6">
    <div class="max-w-400 mx-auto space-y-3 lg:space-y-4">
      <!-- Page header -->
      <div class="flex items-center justify-between gap-3 flex-wrap mb-1">
        <div>
          <h1 class="text-lg font-semibold text-gray-800 dark:text-gray-100">Incident History</h1>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
            Daily failure patterns, per-service health and the full incident log
          </p>
        </div>
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          :loading="refreshing"
          icon="i-mdi-refresh"
          @click="handleRefresh()"
        >
          Refresh
        </UButton>
      </div>

      <ServiceHealthStrip />

      <div
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 frosted-glass px-6 py-5"
      >
        <ErrorCalendar @view-day="handleViewDay" />
      </div>

      <div ref="feedSection" class="scroll-mt-24">
        <EventsFeed
          :date-range="feedDateRange"
          :initial-service-type="initialServiceType"
          :initial-severity="initialSeverity"
          :highlight-id="highlightId"
          @clear-date-range="feedDateRange = null"
        />
      </div>
    </div>
  </div>
</template>
