<script setup lang="ts">
import { ref } from "vue";
import { EntityType } from "@/api/activity";
import type { MetadataRenderer } from "@/composables/useAuditMessage";

const props = defineProps<{
  entityId: string;
  entityType: EntityType;
  metadata: MetadataRenderer;
}>();

const ENTITY_TYPE_LABELS: Record<EntityType, string> = {
  [EntityType.File]: "File",
  [EntityType.FileVersion]: "File Version",
  [EntityType.Directory]: "Folder",
  [EntityType.Tag]: "Tag",
  [EntityType.User]: "User",
  [EntityType.Preview]: "Preview",
};

const copied = ref(false);

const copyEntityId = async () => {
  await navigator.clipboard.writeText(props.entityId);
  copied.value = true;
  setTimeout(() => (copied.value = false), 2000);
};
</script>

<template>
  <div
    class="mt-2 rounded-md bg-white/40 dark:bg-white/5 border border-gray-200/60 dark:border-gray-700/60 px-4 py-3 space-y-3"
  >
    <!-- Diff panel -->
    <AuditDiffPanel v-if="metadata.type === 'diff'" :metadata="metadata" />

    <!-- Divider between diff and entity detail when diff is present -->
    <div
      v-if="metadata.type === 'diff'"
      class="border-t border-gray-200/50 dark:border-gray-700/50"
    />

    <!-- Entity detail -->
    <div class="flex items-center justify-between gap-4 flex-wrap">
      <!-- Entity type label -->
      <div class="flex items-center gap-2">
        <p class="text-xs text-muted">Entity type</p>
        <UBadge color="neutral" variant="subtle" size="xs">
          {{ ENTITY_TYPE_LABELS[entityType] }}
        </UBadge>
      </div>

      <!-- Entity ID with copy -->
      <div class="flex items-center gap-2 min-w-0">
        <p class="text-xs text-muted shrink-0">Entity ID</p>
        <code
          class="text-xs font-mono text-gray-600 dark:text-gray-400 truncate max-w-[200px] sm:max-w-xs"
        >
          {{ entityId }}
        </code>
        <UButton
          :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
          color="neutral"
          variant="ghost"
          size="xs"
          :class="copied ? 'text-emerald-500' : ''"
          :aria-label="copied ? 'Copied' : 'Copy entity ID'"
          @click="copyEntityId"
        />
      </div>
    </div>
  </div>
</template>
