<script setup lang="ts">
import { computed } from "vue";

import type { ServiceType } from "@/enums";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useServiceDashboardRouting } from "@/composables/useServiceDashboardRouting";
import { formatUptime, uptimeClass, useServiceStatus } from "@/composables/useServiceStatus";
import { MONITORED_SERVICES, SERVICE_ICONS, SERVICE_LABELS } from "@/features/monitoring/display";
import { buildDeepLink } from "@/features/monitoring/service-routing";
import { UPTIME_WINDOW_DAYS } from "@/queries/monitoring";
import { formatAgeShort } from "@/utils/date-formatters";

const { activeIncident, healthOf, isLoading, summarize, uptimeOf, visibleError } =
  useServiceStatus();

const rows = computed(() =>
  MONITORED_SERVICES.map((service) => {
    const active = activeIncident(service);

    return {
      // Incident age only makes sense while a service is actually in a bad state.
      age: active ? formatAgeShort(active.createdAt) : null,
      health: healthOf(service),
      icon: SERVICE_ICONS[service],
      label: SERVICE_LABELS[service],
      service,
      uptime: uptimeOf(service),
    };
  }),
);

const overallStatus = computed(() => summarize(MONITORED_SERVICES));

// A card with an active incident deep-links straight to it; healthy cards open the
// service dashboard (or the timeline fallback) bare.
const { goToServiceDetail } = useServiceDashboardRouting();

const openService = (service: ServiceType) => {
  const active = activeIncident(service);

  goToServiceDetail(service, active ? buildDeepLink(active) : {});
};
</script>

<template>
  <div
    class="rounded-[14px] border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
  >
    <div
      class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2 flex-wrap"
    >
      <UIcon name="i-mdi-heart-pulse" class="w-4 h-4 text-gray-600 dark:text-gray-400 shrink-0" />
      <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Service health</h2>

      <UBadge v-if="!isLoading" :color="overallStatus.color" variant="subtle" size="sm">
        {{ overallStatus.label }}
      </UBadge>

      <span class="ml-auto text-xs text-gray-600 dark:text-gray-400">
        Last {{ UPTIME_WINDOW_DAYS }} days
      </span>
    </div>

    <!-- Loading: centered spinner, consistent with the other admin sections -->
    <div v-if="isLoading" class="flex items-center justify-center py-14">
      <BlockSpinner :size="24" aria-label="Loading service health" class="text-gray-500" />
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
          @click="openService(row.service)"
          @keydown.enter.self.prevent="openService(row.service)"
          @keydown.space.self.prevent="openService(row.service)"
        >
          <span class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 bg-gray-500/10">
            <UIcon :name="row.icon" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
          </span>

          <div class="min-w-0 flex-1 flex items-center gap-2">
            <span class="w-2 h-2 rounded-full shrink-0" :class="row.health.dot" />
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
              {{ row.label }}
            </p>
          </div>

          <div class="text-right shrink-0">
            <p class="text-sm font-bold tabular-nums leading-none" :class="uptimeClass(row.uptime)">
              {{ formatUptime(row.uptime) }}
            </p>
            <p class="text-[11px] mt-0.5" :class="row.health.text">{{ row.health.label }}</p>
            <p v-if="row.age" class="text-[11px] mt-0.5 text-gray-600 dark:text-gray-400">
              for {{ row.age }}
            </p>
          </div>
        </div>
      </div>

      <!-- Desktop: grid -->
      <div
        class="hidden sm:grid grid-cols-4 xl:grid-cols-7 divide-x divide-gray-200/60 dark:divide-gray-700/60"
      >
        <div
          v-for="row in rows"
          :key="row.service"
          role="button"
          tabindex="0"
          class="px-4 py-4 flex flex-col gap-2 cursor-pointer transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60"
          :aria-label="`Open ${row.label} details`"
          @click="openService(row.service)"
          @keydown.enter.self.prevent="openService(row.service)"
          @keydown.space.self.prevent="openService(row.service)"
        >
          <div class="flex items-center justify-between gap-2">
            <UIcon :name="row.icon" class="w-4 h-4 text-gray-600 dark:text-gray-400 shrink-0" />
            <span class="w-2 h-2 rounded-full shrink-0" :class="row.health.dot" />
          </div>
          <p class="text-xs font-medium text-gray-900 dark:text-gray-100 truncate">
            {{ row.label }}
          </p>
          <div>
            <p class="text-lg font-bold tabular-nums leading-none" :class="uptimeClass(row.uptime)">
              {{ formatUptime(row.uptime) }}
            </p>
            <p class="text-[11px] mt-1" :class="row.health.text">{{ row.health.label }}</p>
            <p v-if="row.age" class="text-[11px] mt-0.5 text-gray-600 dark:text-gray-400">
              for {{ row.age }}
            </p>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
