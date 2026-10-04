<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

const { payload } = defineProps<{
  payload: Record<string, unknown>;
}>();

const formatted = computed(() => JSON.stringify(payload, null, 2));
</script>

<template>
  <UCollapsible>
    <button
      type="button"
      class="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 dark:text-gray-500 dark:hover:text-gray-300"
    >
      <Icon icon="mdi:code-json" class="w-3.5 h-3.5" />
      View raw payload
    </button>
    <template #content>
      <pre
        class="mt-2 max-h-48 overflow-auto rounded-lg bg-black/5 p-3 font-mono text-[11px] text-gray-600 dark:bg-white/5 dark:text-gray-400"
        >{{ formatted }}</pre>
    </template>
  </UCollapsible>
</template>
