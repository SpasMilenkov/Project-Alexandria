<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, useTemplateRef } from "vue";

import type { AuditExportFormat } from "@/enums/audit-export-format";

import { type AuditExportBounds, activityApi } from "@/api/activity";
import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import ActivityCalendar from "@/components/dashboard/audit/ActivityCalendar.vue";
import AuditExportMenu from "@/components/dashboard/audit/AuditExportMenu.vue";
import AuditFilterBar from "@/components/dashboard/audit/AuditFilterBar.vue";
import AuditTimeline from "@/components/dashboard/audit/AuditTimeline.vue";
import { useAuditRouteFilters } from "@/composables/useAuditRouteFilters";
import {
  AUDIT_PAGE_SIZE,
  PERSONAL_FILTER_FIELDS,
  hasFilterValue,
  toPersonalActivityQuery,
  toPersonalExportFilters,
} from "@/features/audit/query";
import { personalPaginated } from "@/queries/activities";
import { useAuthStore } from "@/stores/auth";

const authStore = useAuthStore();
const viewerId = computed(() => authStore.user?.user.id);

const { filters, setPage } = useAuditRouteFilters();

// The item ID has no control here; it only arrives from a deep link and shows as a pill.
const PERSONAL_CONTROLS = PERSONAL_FILTER_FIELDS.filter((field) => field !== "entityId");

const { data, isPending, error } = useQuery(() =>
  personalPaginated({
    query: toPersonalActivityQuery(filters.value),
    userId: viewerId.value ?? "",
  }),
);

// Token refresh cycles produce transient 401s that resolve on their own.
const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const rows = computed(() => data.value?.items ?? []);

const totalPages = computed(() => {
  if (!data.value) return 1;

  return Math.max(1, Math.ceil(data.value.totalCount / AUDIT_PAGE_SIZE));
});

const isFiltered = computed(() =>
  PERSONAL_FILTER_FIELDS.some((field) => hasFilterValue(filters.value, field)),
);

const onCalendarDay = (range: { from: string; to: string } | null) => {
  filters.value = { ...filters.value, from: range?.from, page: 1, to: range?.to };
};

const timelineSection = useTemplateRef<HTMLElement>("timelineSection");

const changePage = (page: number) => {
  setPage(page);
  timelineSection.value?.scrollIntoView({ behavior: "smooth", block: "start" });
};

const planExport = () => activityApi.planActivityExport(toPersonalExportFilters(filters.value));

const exportActivity = (format: AuditExportFormat, bounds?: AuditExportBounds) =>
  activityApi.exportActivity(toPersonalExportFilters(filters.value), format, bounds);
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-activity" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Activity</h1>
        <UBadge v-if="data" color="neutral" variant="subtle" size="sm">
          {{ data.totalCount.toLocaleString() }}
        </UBadge>
      </div>
    </header>

    <ActivityCalendar
      :entity-type="filters.entityType"
      :from="filters.from"
      :to="filters.to"
      @select-day="onCalendarDay"
    />

    <section
      ref="timelineSection"
      class="scroll-mt-3 rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    >
      <div
        class="flex flex-col gap-3 border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70"
      >
        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-history" class="h-4 w-4 text-gray-600 dark:text-gray-400" />
            <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Timeline</h2>
          </div>

          <p
            v-if="data && totalPages > 1"
            class="text-xs text-gray-600 tabular-nums dark:text-gray-400"
          >
            Page {{ filters.page }} of {{ totalPages }}
          </p>
        </div>

        <AuditFilterBar
          v-model="filters"
          :fields="PERSONAL_FILTER_FIELDS"
          :controls="PERSONAL_CONTROLS"
        >
          <template #actions>
            <AuditExportMenu :plan="planExport" :download="exportActivity" base-name="activity" />
          </template>
        </AuditFilterBar>
      </div>

      <div class="px-3 py-4 md:px-5">
        <UAlert
          v-if="visibleError"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Failed to load activity"
          :description="visibleError.message"
        />

        <div v-else-if="isPending && !data" class="flex h-64 items-center justify-center">
          <BlockSpinner :size="24" aria-label="Loading activity" class="text-gray-500" />
        </div>

        <AuditTimeline v-else-if="rows.length > 0" :rows="rows" :viewer-id="viewerId" />

        <div
          v-else-if="isFiltered"
          class="flex flex-col items-center justify-center gap-3 py-16 text-center"
        >
          <UIcon name="i-lucide-search" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
          <div class="space-y-1">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No matching activity</p>
            <p class="text-xs text-gray-600 dark:text-gray-400">
              Try a wider date range or remove a filter.
            </p>
          </div>
        </div>

        <div v-else class="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <UIcon name="i-lucide-folder-open" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
          <div class="space-y-1">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No activity yet</p>
            <p class="text-xs text-gray-600 dark:text-gray-400">
              Actions will appear here as you use the system
            </p>
          </div>
        </div>
      </div>

      <div
        v-if="data && totalPages > 1"
        class="flex justify-center border-t border-gray-200/70 px-4 py-3 dark:border-gray-700/70"
      >
        <UPagination
          :page="filters.page"
          :total="data.totalCount"
          :items-per-page="AUDIT_PAGE_SIZE"
          :sibling-count="1"
          size="sm"
          @update:page="changePage"
        />
      </div>
    </section>
  </div>
</template>
