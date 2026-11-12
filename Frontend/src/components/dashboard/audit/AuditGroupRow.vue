<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { AuditLogResult } from "@/api/activity";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { auditActorPhrase, describeAuditActor } from "@/features/audit/actor";
import { correlatedActivity } from "@/queries/activities";
import { formatCount } from "@/utils/count";
import { formatClockTime } from "@/utils/date-formatters";

import AuditEventRow from "./AuditEventRow.vue";

const {
  correlationId,
  rows,
  summary,
  title,
  viewerId,
  gone,
  open,
  restorable,
  restoringId = null,
} = defineProps<{
  correlationId: string;
  rows: AuditLogResult[];
  summary: AuditLogResult | null;
  title: string;
  viewerId: string | undefined;
  gone: Set<string>;
  open: boolean;
  restorable: (row: AuditLogResult) => boolean;
  restoringId?: string | null;
}>();

const emit = defineEmits<{
  "update:open": [open: boolean];
  restore: [row: AuditLogResult];
}>();

// The page may hold only part of the request, so expanding loads all of it.
const { data, isPending, error } = useQuery(() => ({
  ...correlatedActivity(correlationId),
  enabled: open,
}));

const newest = computed(() => rows[0]!);

const actor = computed(() => describeAuditActor(summary ?? newest.value, viewerId));

// Newest first, like the rest of the timeline; the summary row is the group's title already.
const members = computed(() => {
  const source = data.value ? [...data.value].reverse() : rows;

  return source.filter((row) => row.id !== summary?.id);
});

const countLabel = computed(() => formatCount(data.value?.length ?? rows.length, "change"));

const openRows = ref<Set<string>>(new Set());

const setRowOpen = (id: string, value: boolean) => {
  const next = new Set(openRows.value);

  if (value) {
    next.add(id);
  } else {
    next.delete(id);
  }

  openRows.value = next;
};

const toggle = () => emit("update:open", !open);
</script>

<template>
  <UCollapsible :open="open" @update:open="(value: boolean) => emit('update:open', value)">
    <div
      role="button"
      tabindex="0"
      :aria-expanded="open"
      class="flex w-full cursor-pointer items-start gap-3 rounded-lg px-2 py-1 text-left transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
      @keydown.enter.self.prevent="toggle"
      @keydown.space.self.prevent="toggle"
    >
      <div class="min-w-0 flex-1">
        <p class="text-sm leading-snug font-medium text-gray-900 sm:truncate dark:text-gray-100">
          {{ title }}
        </p>
        <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
          {{ countLabel }} · {{ auditActorPhrase(actor) }}
        </p>
      </div>

      <div class="flex shrink-0 items-center gap-2 pt-0.5">
        <span class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
          {{ formatClockTime(newest.timestamp) }}
        </span>

        <UBadge color="neutral" variant="subtle" size="sm" class="hidden sm:inline-flex">
          Batch
        </UBadge>

        <UIcon
          name="i-lucide-chevron-down"
          class="h-4 w-4 text-gray-500 transition-transform duration-200 dark:text-gray-500"
          :class="open ? 'rotate-180' : ''"
        />
      </div>
    </div>

    <template #content>
      <div
        class="mt-2 ml-2 flex flex-col gap-1 border-l border-gray-200/70 pl-3 dark:border-gray-700/70"
      >
        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Couldn't load every change from this request"
          description="Showing the changes already on this page."
        />

        <div v-if="isPending && open && !data" class="flex h-16 items-center justify-center">
          <BlockSpinner :size="20" aria-label="Loading changes" class="text-gray-500" />
        </div>

        <template v-else>
          <AuditEventRow
            v-for="member in members"
            :key="member.id"
            :row="member"
            :viewer-id="viewerId"
            :gone="gone"
            :open="openRows.has(member.id)"
            :can-restore="restorable(member)"
            :restoring="restoringId === member.id"
            @update:open="(value: boolean) => setRowOpen(member.id, value)"
            @restore="(row: AuditLogResult) => emit('restore', row)"
          />
        </template>
      </div>
    </template>
  </UCollapsible>
</template>
