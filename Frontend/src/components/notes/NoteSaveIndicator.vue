<script setup lang="ts">
import { computed } from "vue";

import type { SaveIndicatorState } from "@/features/notes/save-indicator";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { formatClockTime } from "@/utils/date-formatters";

const { state, savedAt } = defineProps<{ state: SaveIndicatorState; savedAt: Date | null }>();

const emit = defineEmits<{ retry: [] }>();

const savedTitle = computed(() => {
  if (!savedAt) return undefined;

  return `Saved at ${formatClockTime(savedAt, { seconds: true })}`;
});
</script>

<template>
  <!-- Announced politely, so a screen reader hears "Saved" without losing its place. -->
  <span
    class="flex min-h-8 items-center gap-1.5 text-sm text-gray-600 dark:text-gray-400"
    aria-live="polite"
  >
    <template v-if="state === 'saving'">
      <BlockSpinner :size="14" aria-hidden="true" />
      Saving…
    </template>

    <template v-else-if="state === 'unsaved'">Unsaved changes</template>

    <span v-else-if="state === 'saved'" class="flex items-center gap-1.5" :title="savedTitle">
      <UIcon name="i-lucide-check" class="h-4 w-4" aria-hidden="true" />
      Saved
    </span>

    <template v-else-if="state === 'conflict'">
      <UIcon name="i-lucide-git-compare" class="h-4 w-4 text-warning" aria-hidden="true" />
      Changed elsewhere
    </template>

    <template v-else-if="state === 'error'">
      <UIcon name="i-lucide-triangle-alert" class="h-4 w-4 text-error" aria-hidden="true" />
      Couldn't save
      <UButton size="xs" color="neutral" variant="ghost" label="Retry" @click="emit('retry')" />
    </template>
  </span>
</template>
