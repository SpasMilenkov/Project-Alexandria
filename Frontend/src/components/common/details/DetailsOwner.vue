<template>
  <div
    class="group flex items-center gap-3 rounded-xl border border-gray-200/70 p-3 dark:border-gray-700/70"
  >
    <UAvatar :alt="name" size="md" class="shrink-0" />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">{{ name }}</p>
      <p class="truncate text-sm text-gray-600 dark:text-gray-400">{{ email }}</p>
    </div>

    <DetailsCopyButton :value="email" label="Owner email" />
  </div>
</template>

<script setup lang="ts">
import DetailsCopyButton from "./DetailsCopyButton.vue";

defineProps<{
  name: string;
  email: string;
}>();
</script>
