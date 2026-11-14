<script setup lang="ts">
const { label } = defineProps<{ label: string }>();

defineSlots<{ default: () => unknown }>();
</script>

<template>
  <div
    class="grid grid-cols-[6.5rem_minmax(0,1fr)] gap-4 py-3 text-sm sm:grid-cols-[8rem_minmax(0,1fr)]"
  >
    <dt class="text-gray-600 dark:text-gray-400">{{ label }}</dt>

    <dd class="min-w-0 text-gray-900 dark:text-gray-100">
      <slot />
    </dd>
  </div>
</template>
