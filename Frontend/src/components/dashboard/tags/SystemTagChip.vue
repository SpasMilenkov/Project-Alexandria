<template>
  <span
    class="inline-flex max-w-full items-center gap-2 rounded-full border border-gray-200/70 px-3 py-1.5 dark:border-gray-700/70"
  >
    <span class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: tag.color }" />
    <span
      class="max-w-44 truncate text-xs font-medium text-gray-900 dark:text-gray-100"
      :title="tag.name"
    >
      {{ tag.name }}
    </span>
  </span>
</template>

<script setup lang="ts">
import type { TagDto } from "@/api/tag";

const { tag } = defineProps<{
  tag: TagDto;
}>();
</script>
