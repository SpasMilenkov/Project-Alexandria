<template>
  <div
    class="rounded-[14px] border border-gray-200/70 px-4 py-3 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <p class="flex items-center gap-1.5 text-xs font-medium text-gray-600 dark:text-gray-400">
      <UIcon v-if="icon" :name="icon" class="h-3.5 w-3.5 shrink-0" />
      <span class="truncate">{{ label }}</span>
    </p>

    <div class="mt-1 flex h-8 items-center">
      <BlockSpinner
        v-if="loading"
        :size="16"
        :aria-label="`Loading ${label}`"
        class="text-gray-500"
      />
      <p v-else class="truncate text-2xl font-semibold tabular-nums" :class="TONE_CLASSES[tone]">
        <slot />
      </p>
    </div>

    <p v-if="caption" class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
      {{ caption }}
    </p>
  </div>
</template>

<script setup lang="ts">
import BlockSpinner from "@/components/common/BlockSpinner.vue";

const {
  label,
  caption = null,
  icon = null,
  loading = false,
  tone = "default",
} = defineProps<{
  label: string;
  caption?: string | null;
  icon?: string | null;
  loading?: boolean;
  tone?: "default" | "warning" | "danger";
}>();

const TONE_CLASSES = {
  danger: "text-red-600 dark:text-red-400",
  default: "text-gray-900 dark:text-gray-100",
  warning: "text-amber-600 dark:text-amber-400",
};
</script>
