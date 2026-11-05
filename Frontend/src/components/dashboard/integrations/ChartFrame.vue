<template>
  <!-- A fixed-height, relative box is mandatory with maintainAspectRatio:false, otherwise
       the canvas feedback-loops into unbounded page growth. -->
  <div class="relative" :class="HEIGHTS[height]">
    <div v-if="loading" class="absolute inset-0 grid place-items-center">
      <BlockSpinner :size="24" aria-label="Loading chart" class="text-gray-500" />
    </div>

    <div
      v-else-if="empty"
      class="absolute inset-0 flex flex-col items-center justify-center gap-2 px-6 text-center"
    >
      <UIcon name="i-lucide-chart-line" class="h-10 w-10 text-gray-400 dark:text-gray-600" />
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ emptyTitle }}</p>
      <p v-if="emptyText" class="max-w-sm text-xs text-gray-600 dark:text-gray-400">
        {{ emptyText }}
      </p>
    </div>

    <slot v-else />
  </div>
</template>

<script setup lang="ts">
import BlockSpinner from "@/components/common/BlockSpinner.vue";

const {
  loading = false,
  empty = false,
  emptyTitle = "No data for this period",
  emptyText = null,
  height = "md",
} = defineProps<{
  loading?: boolean;
  empty?: boolean;
  emptyTitle?: string;
  emptyText?: string | null;
  height?: "md" | "lg";
}>();

const HEIGHTS = { lg: "h-72", md: "h-56" };
</script>
