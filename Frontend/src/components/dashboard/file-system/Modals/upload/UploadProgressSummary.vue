<script setup lang="ts">
defineProps<{
  percent: number;
  color: "primary" | "success" | "warning" | "error" | "neutral";
}>();
</script>

<template>
  <div class="border-t border-gray-200/70 px-4 py-3 dark:border-gray-700/70">
    <div
      class="mb-1.5 flex items-center justify-between gap-3 text-xs text-gray-600 dark:text-gray-400"
    >
      <span class="min-w-0 truncate"><slot name="label" /></span>
      <span class="shrink-0 font-medium tabular-nums text-gray-900 dark:text-gray-100">
        {{ percent }}%
      </span>
    </div>

    <UProgress :model-value="percent" :color="color" size="xs" />
  </div>
</template>
