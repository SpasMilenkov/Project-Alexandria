<script setup lang="ts">
defineProps<{ loading: boolean; failed: boolean }>();

const emit = defineEmits<{ retry: [] }>();
</script>

<template>
  <div
    v-if="loading"
    class="flex min-h-0 flex-1 items-center justify-center"
    role="status"
    aria-label="Loading widget data"
  >
    <UIcon name="i-mdi-loading" class="size-6 animate-spin text-gray-500 dark:text-gray-500" />
  </div>

  <div
    v-else-if="failed"
    class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
    role="alert"
  >
    <p class="text-sm text-gray-600 dark:text-gray-400">Could not load this widget.</p>

    <UButton color="neutral" variant="outline" size="sm" @click="emit('retry')">Try again</UButton>
  </div>

  <slot v-else />
</template>
