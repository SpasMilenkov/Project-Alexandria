<template>
  <p
    class="flex items-start gap-2 rounded-xl bg-gray-500/10 px-3 py-2 text-sm text-gray-900 dark:text-gray-100"
  >
    <UIcon
      :name="icon"
      class="mt-0.5 size-4 shrink-0 text-gray-600 dark:text-gray-400"
      aria-hidden="true"
    />
    <span><slot /></span>
  </p>
</template>

<script setup lang="ts">
const { icon } = defineProps<{
  icon: string;
}>();
</script>
