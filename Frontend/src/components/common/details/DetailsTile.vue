<template>
  <div
    class="group flex min-w-0 flex-col gap-1 rounded-xl border border-gray-200/70 p-3 dark:border-gray-700/70"
  >
    <div class="flex min-h-6 items-center justify-between gap-2">
      <span class="text-xs font-medium text-gray-600 dark:text-gray-400">{{ label }}</span>

      <DetailsCopyButton v-if="copyValue" :value="copyValue" :label="label" />
    </div>

    <div class="min-w-0 text-sm font-medium text-gray-900 dark:text-gray-100">
      <slot />
    </div>

    <p v-if="hint" class="text-xs text-gray-500 dark:text-gray-500">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import DetailsCopyButton from "./DetailsCopyButton.vue";

const { copyValue = undefined, hint = undefined } = defineProps<{
  label: string;
  copyValue?: string;
  hint?: string;
}>();
</script>
