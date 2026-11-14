<template>
  <div
    class="flex min-w-0 flex-col gap-0.5 rounded-lg px-2.5 py-2"
    :class="{ 'bg-white shadow-sm dark:bg-neutral-800': emphasis }"
  >
    <dt class="text-xs text-gray-600 dark:text-gray-400">{{ label }}</dt>

    <dd class="truncate text-sm font-semibold tabular-nums text-gray-900 dark:text-gray-100">
      <slot />
    </dd>

    <dd v-if="hint" class="truncate text-xs text-gray-500 dark:text-gray-500">{{ hint }}</dd>
  </div>
</template>

<script setup lang="ts">
// The emphasized tile is lifted out of the panel, the way the vitals hints mark the
// current level, so the one number a reader came for stands out.
const {
  label,
  hint = undefined,
  emphasis = false,
} = defineProps<{
  label: string;
  hint?: string;
  emphasis?: boolean;
}>();
</script>
