<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, useTemplateRef } from "vue";

import { EntityType, LogSource, OperationType } from "@/api/activity";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import ActivityCalendar from "@/components/dashboard/audit/ActivityCalendar.vue";
import { useAuditMessage } from "@/composables/useAuditMessage";
import { SortDirection } from "@/enums/SortDirection";
import { personalPaginated } from "@/queries/activities";
import { useActivityStore } from "@/stores/activity";
import { useAuthStore } from "@/stores/auth";
import { formatCount } from "@/utils/count";
import { formatClockTime, formatDayLabel, localDayKey } from "@/utils/date-formatters";

const activityStore = useActivityStore();
const authStore = useAuthStore();
const { getMessage, getRenderedMetadata } = useAuditMessage();

const { data, isPending, error } = useQuery(() =>
  personalPaginated({
    page: activityStore.page,
    pageSize: activityStore.pageSize,
    sortBy: "timestamp",
    sortDirection: SortDirection.Desc,
    userId: authStore.user?.user.id,
  }),
);

// Token refresh cycles produce transient 401s that resolve on their own.
const isAuthError = (err: unknown): boolean => {
  if (!err) return false;

  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;

  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;
  return error.value;
});

type OpColor = "success" | "error" | "warning" | "info" | "neutral";

const OP_COLORS: Partial<Record<OperationType, OpColor>> = {
  [OperationType.Read]: "neutral",
  [OperationType.Create]: "success",
  [OperationType.Delete]: "error",
  [OperationType.Update]: "warning",
  [OperationType.Login]: "info",
  [OperationType.Logout]: "neutral",
};

const NEUTRAL_INDICATOR =
  "bg-gray-500/10 text-gray-600 ring-1 ring-inset ring-gray-400/30 dark:text-gray-400 dark:ring-gray-500/30";

const OP_INDICATORS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]:
    "bg-emerald-500/10 text-emerald-600 ring-1 ring-inset ring-emerald-500/30 dark:text-emerald-400",
  [OperationType.Delete]:
    "bg-rose-500/10 text-rose-600 ring-1 ring-inset ring-rose-500/30 dark:text-rose-400",
  [OperationType.Update]:
    "bg-amber-500/10 text-amber-600 ring-1 ring-inset ring-amber-500/30 dark:text-amber-400",
  [OperationType.Login]:
    "bg-sky-500/10 text-sky-600 ring-1 ring-inset ring-sky-500/30 dark:text-sky-400",
};

const LOG_SOURCE_LABELS: Record<LogSource, string> = {
  [LogSource.API]: "API",
  [LogSource.Trigger]: "Automated",
  [LogSource.System]: "System",
};

const ENTITY_LABELS: Record<EntityType, string> = {
  [EntityType.File]: "File",
  [EntityType.FileVersion]: "File version",
  [EntityType.Directory]: "Folder",
  [EntityType.Tag]: "Tag",
  [EntityType.User]: "Account",
  [EntityType.Preview]: "Preview",
};

const ENTITY_ICONS: Partial<Record<EntityType, string>> = {
  [EntityType.Directory]: "i-lucide-folder",
  [EntityType.File]: "i-lucide-file",
  [EntityType.FileVersion]: "i-lucide-file-clock",
  [EntityType.Preview]: "i-lucide-image",
  [EntityType.Tag]: "i-lucide-tag",
  [EntityType.User]: "i-lucide-user",
};

const OP_ICONS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]: "i-lucide-plus-circle",
  [OperationType.Update]: "i-lucide-edit",
  [OperationType.Delete]: "i-lucide-trash-2",
  [OperationType.Login]: "i-lucide-log-in",
  [OperationType.Logout]: "i-lucide-log-out",
};

const getIcon = (op: OperationType, entity: EntityType): string =>
  ENTITY_ICONS[entity] ?? OP_ICONS[op] ?? "i-lucide-eye";

const rows = computed(() => {
  if (!data.value) return [];

  return data.value.items.map((log) => {
    const at = new Date(log.timestamp);

    return {
      color: OP_COLORS[log.operationType] ?? "neutral",
      dayKey: localDayKey(at),
      entityId: log.entityId,
      entityLabel: ENTITY_LABELS[log.entityType] ?? "Item",
      entityType: log.entityType,
      icon: getIcon(log.operationType, log.entityType),
      key: log.entityId + log.timestamp,
      logSourceLabel: LOG_SOURCE_LABELS[log.logSource] ?? "Unknown",
      metadata: getRenderedMetadata(log.eventCode, log.metadata),
      opLabel: OperationType[log.operationType] ?? "Unknown",
      time: formatClockTime(at),
      timestamp: log.timestamp,
      title: getMessage(log.eventCode),
      ui: { indicator: OP_INDICATORS[log.operationType] ?? NEUTRAL_INDICATOR },
    };
  });
});

type ActivityRow = (typeof rows.value)[number];

const dayGroups = computed(() => {
  const groups: { key: string; label: string; rows: ActivityRow[] }[] = [];

  for (const row of rows.value) {
    const last = groups.at(-1);

    if (last?.key === row.dayKey) {
      last.rows.push(row);
    } else {
      groups.push({ key: row.dayKey, label: formatDayLabel(row.timestamp), rows: [row] });
    }
  }

  return groups;
});

const totalPages = computed(() => {
  if (!data.value) return 1;
  return Math.max(1, Math.ceil(data.value.totalCount / activityStore.pageSize));
});

const expandedKeys = ref<Set<string>>(new Set());

const setExpanded = (key: string, open: boolean) => {
  const next = new Set(expandedKeys.value);

  if (open) {
    next.add(key);
  } else {
    next.delete(key);
  }

  expandedKeys.value = next;
};

const timelineSection = useTemplateRef<HTMLElement>("timelineSection");

const changePage = (pageNumber: number) => {
  activityStore.page = pageNumber;
  expandedKeys.value = new Set();
  timelineSection.value?.scrollIntoView({ behavior: "smooth", block: "start" });
};

const TIMELINE_UI = {
  separator: "bg-gray-200/70 dark:bg-gray-700/70",
  wrapper: "mt-0 min-w-0 pb-3 group-last:pb-0",
};
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

    <ActivityCalendar />

    <section
      ref="timelineSection"
      class="scroll-mt-3 rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    >
      <div
        class="flex items-center justify-between gap-3 border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70"
      >
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-history" class="h-4 w-4 text-gray-600 dark:text-gray-400" />
          <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Timeline</h2>
        </div>

        <p
          v-if="data && totalPages > 1"
          class="text-xs text-gray-500 tabular-nums dark:text-gray-500"
        >
          Page {{ activityStore.page }} of {{ totalPages }}
        </p>
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

        <div v-else-if="isPending" class="flex h-64 items-center justify-center">
          <BlockSpinner :size="24" aria-label="Loading activity" class="text-gray-500" />
        </div>

        <div v-else-if="dayGroups.length > 0" class="flex flex-col gap-5">
          <div v-for="group in dayGroups" :key="group.key" class="flex flex-col gap-3">
            <div class="flex items-baseline gap-2 px-1">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">
                {{ group.label }}
              </h3>
              <span class="text-xs text-gray-500 tabular-nums dark:text-gray-500">
                {{ formatCount(group.rows.length, "event") }}
              </span>
            </div>

            <UTimeline :items="group.rows" size="md" color="neutral" :ui="TIMELINE_UI">
              <template #wrapper="{ item }">
                <UCollapsible
                  :open="expandedKeys.has(item.key)"
                  @update:open="(open: boolean) => setExpanded(item.key, open)"
                >
                  <button
                    type="button"
                    class="flex w-full items-start gap-3 rounded-lg px-2 py-1 text-left transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
                  >
                    <div class="min-w-0 flex-1">
                      <p
                        class="text-sm leading-snug font-medium text-gray-900 sm:truncate dark:text-gray-100"
                      >
                        {{ item.title }}
                      </p>
                      <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
                        {{ item.entityLabel }} · {{ item.logSourceLabel }}
                      </p>
                    </div>

                    <div class="flex shrink-0 items-center gap-2 pt-0.5">
                      <span class="text-xs text-gray-500 tabular-nums dark:text-gray-500">
                        {{ item.time }}
                      </span>

                      <UBadge :color="item.color" variant="subtle" size="sm">
                        {{ item.opLabel }}
                      </UBadge>

                      <UIcon
                        name="i-lucide-chevron-down"
                        class="h-4 w-4 text-gray-500 transition-transform duration-200 dark:text-gray-500"
                        :class="expandedKeys.has(item.key) ? 'rotate-180' : ''"
                      />
                    </div>
                  </button>

                  <template #content>
                    <div class="px-2 pt-2 pb-1">
                      <AuditDetailPanel
                        :entity-id="item.entityId"
                        :entity-type="item.entityType"
                        :metadata="item.metadata"
                        :timestamp="item.timestamp"
                        :source-label="item.logSourceLabel"
                      />
                    </div>
                  </template>
                </UCollapsible>
              </template>
            </UTimeline>
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
          :page="activityStore.page"
          :total="data.totalCount"
          :items-per-page="activityStore.pageSize"
          :sibling-count="1"
          size="sm"
          @update:page="changePage"
        />
      </div>
    </section>
  </div>
</template>
