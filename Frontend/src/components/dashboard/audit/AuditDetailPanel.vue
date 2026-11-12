<script setup lang="ts">
import { computed, ref } from "vue";
import { type RouteLocationRaw, RouterLink } from "vue-router";

import type { AuditLogResult } from "@/api/activity";
import type { AuditActor } from "@/features/audit/actor";

import { getAuditDiff, getAuditMetadataEntries } from "@/composables/useAuditMessage";
import { AuditEventCode } from "@/enums/audit-event-code";
import { EntityType } from "@/enums/entity-type";
import { entityLabel, sourceLabel } from "@/features/audit/presentation";
import { formatDateTime } from "@/utils/date-formatters";
import { logger } from "@/utils/logger";

import AuditDiffPanel from "./AuditDiffPanel.vue";

const {
  row,
  actor,
  name,
  entityRoute,
  canRestore = false,
  restoring = false,
  requestLink = null,
} = defineProps<{
  row: AuditLogResult;
  actor: AuditActor;
  name: string | null;
  // Folders, playlists and tags of the viewer's own; files have no page to open.
  entityRoute: RouteLocationRaw | null;
  canRestore?: boolean;
  restoring?: boolean;
  // Admin explorer only: narrows the explorer to every row of this request.
  requestLink?: RouteLocationRaw | null;
}>();

const emit = defineEmits<{
  restore: [];
}>();

// Sign-in and session rows are where an address means something to the reader.
const IP_EVENTS = new Set([
  AuditEventCode.UserLogin,
  AuditEventCode.UserLoginFailed,
  AuditEventCode.UserLogout,
  AuditEventCode.UserSessionRevoked,
  AuditEventCode.ShareLinkAccessed,
]);

const diff = computed(() => getAuditDiff(row.eventCode, row.metadata));

const details = computed(() => {
  const entries = getAuditMetadataEntries(row.metadata);

  // File tag rows are named after the file, so the tag itself goes in the details.
  if (row.entityType === EntityType.FileTag && row.entityName) {
    entries.unshift({ label: "Tag", value: row.entityName });
  }

  if (row.fallbackDescription) entries.push({ label: "Note", value: row.fallbackDescription });

  return entries;
});

const recordedAt = computed(() => formatDateTime(row.timestamp, { seconds: true }));

const showIp = computed(() => Boolean(row.ipAddress) && IP_EVENTS.has(row.eventCode));

const actorText = computed(() => {
  if (actor.kind === "self") return "You";

  return actor.label;
});

const copied = ref(false);

const copyEntityId = async () => {
  try {
    await navigator.clipboard.writeText(row.entityId);
    copied.value = true;
    setTimeout(() => (copied.value = false), 2000);
  } catch (err) {
    logger.warn("Could not copy the entity id", err);
  }
};
</script>

<template>
  <div class="flex flex-col gap-3">
    <dl
      class="grid grid-cols-1 gap-x-6 gap-y-3 rounded-lg border border-gray-200/70 bg-gray-500/5 px-4 py-3 text-sm sm:grid-cols-[max-content_minmax(0,1fr)] sm:gap-y-2.5 dark:border-gray-700/70 dark:bg-gray-400/5"
    >
      <template v-if="diff.type === 'diff'">
        <dt class="text-xs font-medium text-gray-600 sm:pt-1 dark:text-gray-400">
          {{ diff.label }}
        </dt>
        <dd class="-mt-2 min-w-0 sm:mt-0">
          <AuditDiffPanel :metadata="diff" />
        </dd>
      </template>

      <template v-for="entry in details" :key="entry.label">
        <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">
          {{ entry.label }}
        </dt>
        <dd class="-mt-2 min-w-0 break-words text-gray-900 sm:mt-0 dark:text-gray-100">
          {{ entry.value }}
        </dd>
      </template>

      <template v-if="showIp">
        <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">IP address</dt>
        <dd class="-mt-2 font-mono text-xs text-gray-900 sm:mt-0 sm:pt-0.5 dark:text-gray-100">
          {{ row.ipAddress }}
        </dd>
      </template>

      <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">Done by</dt>
      <dd class="-mt-2 text-gray-900 sm:mt-0 dark:text-gray-100">
        {{ actorText }}
        <span v-if="actor.affectsViewer" class="text-gray-600 dark:text-gray-400">
          · on your account
        </span>
      </dd>

      <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">Recorded</dt>
      <dd class="-mt-2 text-gray-900 tabular-nums sm:mt-0 dark:text-gray-100">
        {{ recordedAt }}
        <span class="text-gray-600 dark:text-gray-400">· {{ sourceLabel(row.logSource) }}</span>
      </dd>

      <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">
        {{ entityLabel(row.entityType) }}
      </dt>
      <dd class="-mt-2 flex min-w-0 flex-col gap-1 sm:mt-0">
        <span v-if="name" class="min-w-0 break-words">
          <RouterLink
            v-if="entityRoute"
            :to="entityRoute"
            class="font-medium text-gray-900 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
          >
            {{ name }}
          </RouterLink>

          <span v-else class="font-medium text-gray-900 dark:text-gray-100">{{ name }}</span>
        </span>

        <span class="flex min-w-0 items-center gap-2">
          <code class="min-w-0 truncate font-mono text-xs text-gray-600 dark:text-gray-400">
            {{ row.entityId }}
          </code>

          <UButton
            :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
            color="neutral"
            variant="ghost"
            size="xs"
            class="shrink-0 justify-center"
            :class="copied ? 'text-emerald-600 dark:text-emerald-400' : ''"
            :aria-label="copied ? 'Copied' : 'Copy ID'"
            @click="copyEntityId"
          />
        </span>
      </dd>
    </dl>

    <div v-if="canRestore || requestLink" class="flex flex-wrap items-center gap-2">
      <UButton
        v-if="canRestore"
        icon="i-lucide-undo-2"
        label="Restore"
        size="xs"
        color="neutral"
        variant="outline"
        :loading="restoring"
        @click="emit('restore')"
      />

      <UButton
        v-if="requestLink"
        :to="requestLink"
        icon="i-lucide-layers"
        label="Everything from this request"
        size="xs"
        color="neutral"
        variant="ghost"
      />
    </div>
  </div>
</template>
