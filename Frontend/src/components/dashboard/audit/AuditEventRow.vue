<script setup lang="ts">
import type { RouteLocationRaw } from "vue-router";

import { computed } from "vue";

import type { AuditLogResult } from "@/api/activity";

import { getAuditMessage } from "@/composables/useAuditMessage";
import { auditActorPhrase, describeAuditActor } from "@/features/audit/actor";
import { auditEntityName, auditEntityRoute } from "@/features/audit/links";
import { entityLabel, operationColor, operationLabel } from "@/features/audit/presentation";
import { formatClockTime } from "@/utils/date-formatters";

import AuditDetailPanel from "./AuditDetailPanel.vue";

const {
  row,
  viewerId,
  gone,
  open,
  canRestore = false,
  restoring = false,
  requestLink = null,
} = defineProps<{
  row: AuditLogResult;
  viewerId: string | undefined;
  gone: Set<string>;
  open: boolean;
  canRestore?: boolean;
  restoring?: boolean;
  requestLink?: RouteLocationRaw | null;
}>();

const emit = defineEmits<{
  "update:open": [open: boolean];
  restore: [row: AuditLogResult];
}>();

const actor = computed(() => describeAuditActor(row, viewerId));
const name = computed(() => auditEntityName(row));
const entityRoute = computed(() => auditEntityRoute(row, viewerId, gone));

// The collapsible trigger already toggles on click; a div row needs the keys a button gets.
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
          {{ getAuditMessage(row.eventCode) }}
        </p>

        <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
          <span v-if="name" class="font-medium text-gray-700 dark:text-gray-300">
            {{ name }}
          </span>

          <span v-else>{{ entityLabel(row.entityType) }}</span>

          <span> · {{ auditActorPhrase(actor) }}</span>
        </p>
      </div>

      <div class="flex shrink-0 items-center gap-2 pt-0.5">
        <span class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
          {{ formatClockTime(row.timestamp) }}
        </span>

        <UBadge
          :color="operationColor(row.operationType)"
          variant="subtle"
          size="sm"
          class="hidden sm:inline-flex"
        >
          {{ operationLabel(row.operationType) }}
        </UBadge>

        <UIcon
          name="i-lucide-chevron-down"
          class="h-4 w-4 text-gray-500 transition-transform duration-200 dark:text-gray-500"
          :class="open ? 'rotate-180' : ''"
        />
      </div>
    </div>

    <template #content>
      <div class="px-2 pt-2 pb-1">
        <AuditDetailPanel
          :row="row"
          :actor="actor"
          :name="name"
          :entity-route="entityRoute"
          :can-restore="canRestore"
          :restoring="restoring"
          :request-link="requestLink"
          @restore="emit('restore', row)"
        />
      </div>
    </template>
  </UCollapsible>
</template>
