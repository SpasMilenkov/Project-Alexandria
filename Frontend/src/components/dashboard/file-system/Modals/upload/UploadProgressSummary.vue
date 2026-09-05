<script setup lang="ts">
defineProps<{
  percent: number;
  color: "primary" | "success" | "warning" | "error" | "neutral";
}>();
</script>

<template>
  <div
    class="px-4 py-3 border-t border-gray-200/70 dark:border-gray-700/70 frosted-glass bg-white/40 dark:bg-white/3"
  >
    <div class="flex items-center justify-between text-xs text-muted mb-1.5">
      <slot name="label" />
      <span class="tabular-nums font-medium">{{ percent }}%</span>
    </div>
    <UProgress :value="percent" :color="color" size="xs" />
  </div>
</template>
