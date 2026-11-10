<script setup lang="ts">
import { computed, ref } from "vue";

import type { MetadataRenderer } from "@/composables/useAuditMessage";

import { EntityType } from "@/api/activity";
import { formatDateTime } from "@/utils/date-formatters";

const { entityId, entityType, metadata, timestamp, sourceLabel } = defineProps<{
  entityId: string;
  entityType: EntityType;
  metadata: MetadataRenderer;
  timestamp: string;
  sourceLabel: string;
}>();

const ENTITY_TYPE_LABELS: Record<EntityType, string> = {
  [EntityType.File]: "File",
  [EntityType.FileVersion]: "File version",
  [EntityType.Directory]: "Folder",
  [EntityType.Tag]: "Tag",
  [EntityType.User]: "User",
  [EntityType.Preview]: "Preview",
};

const recordedAt = computed(() => formatDateTime(timestamp, { seconds: true }));

const copied = ref(false);

const copyEntityId = async () => {
  await navigator.clipboard.writeText(entityId);
  copied.value = true;
  setTimeout(() => (copied.value = false), 2000);
};
</script>

<template>
  <dl
    class="grid grid-cols-1 gap-x-6 gap-y-3 rounded-lg border border-gray-200/70 bg-gray-500/5 px-4 py-3 text-sm sm:grid-cols-[max-content_minmax(0,1fr)] sm:gap-y-2.5 dark:border-gray-700/70 dark:bg-gray-400/5"
  >
    <template v-if="metadata.type === 'diff'">
      <dt class="text-xs font-medium text-gray-600 sm:pt-1 dark:text-gray-400">
        {{ metadata.label }}
      </dt>
      <dd class="-mt-2 min-w-0 sm:mt-0">
        <AuditDiffPanel :metadata="metadata" />
      </dd>
    </template>

    <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">Recorded</dt>
    <dd class="-mt-2 text-gray-900 tabular-nums sm:mt-0 dark:text-gray-100">
      {{ recordedAt }}
      <span class="text-gray-500 dark:text-gray-500">· {{ sourceLabel }}</span>
    </dd>

    <dt class="text-xs font-medium text-gray-600 sm:pt-0.5 dark:text-gray-400">Entity</dt>
    <dd class="-mt-2 flex min-w-0 items-center gap-2 sm:mt-0">
      <UBadge color="neutral" variant="subtle" size="sm" class="shrink-0">
        {{ ENTITY_TYPE_LABELS[entityType] ?? "Unknown" }}
      </UBadge>

      <code class="min-w-0 truncate font-mono text-xs text-gray-600 dark:text-gray-400">
        {{ entityId }}
      </code>

      <UButton
        :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
        color="neutral"
        variant="ghost"
        size="xs"
        class="shrink-0 justify-center"
        :class="copied ? 'text-emerald-600 dark:text-emerald-400' : ''"
        :aria-label="copied ? 'Copied' : 'Copy entity ID'"
        @click="copyEntityId"
      />
    </dd>
  </dl>
</template>
