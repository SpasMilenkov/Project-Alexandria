<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, useTemplateRef, watch } from "vue";

import type { AuditExportBounds } from "@/api/activity";
import type { AuditExportFormat } from "@/enums/audit-export-format";

import { adminAuditApi } from "@/api/adminAudit";
import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import AccessViewTabs from "@/components/dashboard/admin/access/AccessViewTabs.vue";
import AuditExportMenu from "@/components/dashboard/audit/AuditExportMenu.vue";
import AuditFilterBar from "@/components/dashboard/audit/AuditFilterBar.vue";
import AuditTimeline from "@/components/dashboard/audit/AuditTimeline.vue";
import { useAuditRouteFilters } from "@/composables/useAuditRouteFilters";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import {
  ADMIN_ENTITY_TYPE_OPTIONS,
  ADMIN_EVENT_CODES,
  ADMIN_OPERATION_OPTIONS,
} from "@/features/audit/presentation";
import {
  ADMIN_FILTER_FIELDS,
  AUDIT_PAGE_SIZE,
  hasFilterValue,
  toAdminAuditFilterRequest,
  toAdminAuditQuery,
} from "@/features/audit/query";
import { adminAuditSearch } from "@/queries/adminAudit";
import { useAuthStore } from "@/stores/auth";

const LIVE_INTERVAL_MS = 15_000;

const authStore = useAuthStore();
const viewerId = computed(() => authStore.user?.user.id);

const { filters, setPage } = useAuditRouteFilters();

const { data, isPending, error, refetch } = useQuery(() =>
  adminAuditSearch(toAdminAuditQuery(filters.value)),
);

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
  ADMIN_FILTER_FIELDS.some((field) => hasFilterValue(filters.value, field)),
);

// Live mode only makes sense on the newest page; older pages would shift under the reader.
const isLive = ref(false);

const { pause, resume } = useLiveRefresh(() => {
  if (isLive.value && filters.value.page === 1) void refetch();
}, LIVE_INTERVAL_MS);

watch(
  isLive,
  (live) => {
    if (live) {
      resume();
      void refetch();
      return;
    }

    pause();
  },
  { immediate: true },
);

const timelineSection = useTemplateRef<HTMLElement>("timelineSection");

const changePage = (page: number) => {
  setPage(page);
  timelineSection.value?.scrollIntoView({ behavior: "smooth", block: "start" });
};

const planExport = () => adminAuditApi.planAuditExport(toAdminAuditFilterRequest(filters.value));

const exportAudit = (format: AuditExportFormat, bounds?: AuditExportBounds) =>
  adminAuditApi.exportAudit(toAdminAuditFilterRequest(filters.value), format, bounds);
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-shield-check"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          Access & security
        </h1>
        <UBadge v-if="data" color="neutral" variant="subtle" size="sm">
          {{ data.totalCount.toLocaleString() }}
        </UBadge>
      </div>

      <div class="flex items-center gap-3">
        <USwitch v-model="isLive" label="Live" size="sm" color="primary" />
        <AccessViewTabs />
      </div>
    </header>

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

        <p class="flex items-start gap-2 text-xs text-gray-600 dark:text-gray-400">
          <UIcon name="i-lucide-eye-off" class="mt-px h-3.5 w-3.5 shrink-0" />
          Libraries stay private, yours included: files, folders, tags, playlists, share links and
          personal settings never appear here. This log covers sign-ins, accounts, roles, quotas,
          sessions, jobs, incidents and system changes. Your own library history is in Access
          History.
        </p>

        <AuditFilterBar
          v-model="filters"
          :fields="ADMIN_FILTER_FIELDS"
          :entity-type-options="ADMIN_ENTITY_TYPE_OPTIONS"
          :operation-options="ADMIN_OPERATION_OPTIONS"
          :event-codes="ADMIN_EVENT_CODES"
        >
          <template #actions>
            <AuditExportMenu :plan="planExport" :download="exportAudit" base-name="audit" />
          </template>
        </AuditFilterBar>
      </div>

      <div class="px-3 py-4 md:px-5">
        <UAlert
          v-if="visibleError"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Failed to load the activity log"
          :description="visibleError.message"
        />

        <div v-else-if="isPending && !data" class="flex h-64 items-center justify-center">
          <BlockSpinner :size="24" aria-label="Loading the activity log" class="text-gray-500" />
        </div>

        <AuditTimeline
          v-else-if="rows.length > 0"
          :rows="rows"
          :viewer-id="viewerId"
          request-links
        />

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
          <UIcon name="i-lucide-history" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
          <div class="space-y-1">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Nothing recorded yet</p>
            <p class="text-xs text-gray-600 dark:text-gray-400">
              Sign-ins, account changes and system events will appear here.
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
