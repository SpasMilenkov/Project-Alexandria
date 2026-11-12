<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { AuditLogResult } from "@/api/activity";

import { useAppToast } from "@/composables/useAppToast";
import { type AuditTimelineItem, buildAuditTimeline } from "@/features/audit/grouping";
import { auditEntityName, collectGoneEntityIds } from "@/features/audit/links";
import { auditIcon, auditIndicatorClass } from "@/features/audit/presentation";
import { auditRestoreTarget, canRestoreFromAudit } from "@/features/audit/restore";
import { quoted } from "@/features/toast/format";
import { restoreFromAudit } from "@/mutations/activity";
import { formatCount } from "@/utils/count";
import { formatDayLabel, localDayKey } from "@/utils/date-formatters";

import AuditEventRow from "./AuditEventRow.vue";
import AuditGroupRow from "./AuditGroupRow.vue";

const {
  rows,
  viewerId,
  grouped = true,
  compact = false,
  requestLinks = false,
} = defineProps<{
  rows: AuditLogResult[];
  viewerId: string | undefined;
  // Collapses consecutive rows of one request into a summary row.
  grouped?: boolean;
  // Tighter spacing for drawers.
  compact?: boolean;
  // Admin explorer: rows offer a link to every row of their request.
  requestLinks?: boolean;
}>();

const appToast = useAppToast("activity");

const gone = computed(() => collectGoneEntityIds(rows));

interface TimelineEntry {
  key: string;
  icon: string;
  item: AuditTimelineItem;
  ui: { indicator: string };
}

const toEntry = (item: AuditTimelineItem): TimelineEntry => {
  if (item.kind === "group") {
    const first = item.summary ?? item.rows[0]!;

    return {
      icon: auditIcon(first.eventCode, first.entityType),
      item,
      key: item.key,
      ui: { indicator: auditIndicatorClass(first.eventCode, first.operationType) },
    };
  }

  return {
    icon: auditIcon(item.row.eventCode, item.row.entityType),
    item,
    key: item.key,
    ui: { indicator: auditIndicatorClass(item.row.eventCode, item.row.operationType) },
  };
};

const toItems = (dayRows: AuditLogResult[]): AuditTimelineItem[] => {
  if (grouped) return buildAuditTimeline(dayRows);

  return dayRows.map((row) => ({ key: row.id, kind: "event", row }));
};

const dayGroups = computed(() => {
  const days: { key: string; label: string; rows: AuditLogResult[] }[] = [];

  for (const row of rows) {
    const key = localDayKey(new Date(row.timestamp));
    const last = days.at(-1);

    if (last?.key === key) {
      last.rows.push(row);
    } else {
      days.push({ key, label: formatDayLabel(row.timestamp), rows: [row] });
    }
  }

  return days.map((day) => ({
    ...day,
    entries: toItems(day.rows).map(toEntry),
  }));
});

const expandedKeys = ref<Set<string>>(new Set());

// A new page or filter starts collapsed.
watch(
  () => rows,
  () => {
    expandedKeys.value = new Set();
  },
);

const setExpanded = (key: string, open: boolean) => {
  const next = new Set(expandedKeys.value);

  if (open) {
    next.add(key);
  } else {
    next.delete(key);
  }

  expandedKeys.value = next;
};

const restorable = (row: AuditLogResult) => canRestoreFromAudit(row, rows, viewerId);

const requestLinkFor = (row: AuditLogResult) => {
  if (!requestLinks || !row.correlationId) return null;

  return { name: "admin-activity-log", query: { correlationId: row.correlationId } };
};

const { mutateAsync: restore } = restoreFromAudit();
const restoringId = ref<string | null>(null);

const handleRestore = async (row: AuditLogResult) => {
  const target = auditRestoreTarget(row);
  if (!target || restoringId.value) return;

  const name = auditEntityName(row);
  const label = name ? quoted(name) : "the item";

  restoringId.value = row.id;
  try {
    await restore(target);
    appToast.success(`Restored ${label}`);
  } catch (error) {
    appToast.error(`Couldn't restore ${label}`, error);
  } finally {
    restoringId.value = null;
  }
};

const TIMELINE_UI = {
  separator: "bg-gray-200/70 dark:bg-gray-700/70",
  wrapper: "mt-0 min-w-0 pb-3 group-last:pb-0",
};

const COMPACT_TIMELINE_UI = {
  separator: "bg-gray-200/70 dark:bg-gray-700/70",
  wrapper: "mt-0 min-w-0 pb-2 group-last:pb-0",
};
</script>

<template>
  <div class="flex flex-col" :class="compact ? 'gap-4' : 'gap-5'">
    <div v-for="day in dayGroups" :key="day.key" class="flex flex-col gap-3">
      <div class="flex items-baseline gap-2 px-1">
        <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">
          {{ day.label }}
        </h3>
        <span class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
          {{ formatCount(day.rows.length, "event") }}
        </span>
      </div>

      <UTimeline
        :items="day.entries"
        :size="compact ? 'sm' : 'md'"
        color="neutral"
        :ui="compact ? COMPACT_TIMELINE_UI : TIMELINE_UI"
      >
        <template #wrapper="{ item: entry }">
          <AuditGroupRow
            v-if="entry.item.kind === 'group'"
            :correlation-id="entry.item.correlationId"
            :rows="entry.item.rows"
            :summary="entry.item.summary"
            :title="entry.item.title"
            :viewer-id="viewerId"
            :gone="gone"
            :open="expandedKeys.has(entry.key)"
            :restorable="restorable"
            :restoring-id="restoringId"
            @update:open="(open: boolean) => setExpanded(entry.key, open)"
            @restore="handleRestore"
          />

          <AuditEventRow
            v-else
            :row="entry.item.row"
            :viewer-id="viewerId"
            :gone="gone"
            :open="expandedKeys.has(entry.key)"
            :can-restore="restorable(entry.item.row)"
            :restoring="restoringId === entry.item.row.id"
            :request-link="requestLinkFor(entry.item.row)"
            @update:open="(open: boolean) => setExpanded(entry.key, open)"
            @restore="handleRestore"
          />
        </template>
      </UTimeline>
    </div>
  </div>
</template>
