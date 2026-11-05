<template>
  <span
    class="inline-flex h-6 max-w-56 items-center gap-1.5 rounded-full border border-gray-200/70 px-2.5 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
  >
    <span
      v-if="swatch"
      class="h-2 w-2 shrink-0 rounded-full"
      :style="{ backgroundColor: swatch }"
    />
    <span class="truncate"><slot /></span>
  </span>
</template>

<script setup lang="ts">
const { swatch = null } = defineProps<{
  swatch?: string | null;
}>();
</script>
