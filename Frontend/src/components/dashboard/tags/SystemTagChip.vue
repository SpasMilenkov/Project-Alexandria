<template>
  <div
    class="inline-flex items-center gap-2 rounded-lg border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 px-3 py-2 transition-colors duration-200 ease-out hover:border-gray-300 dark:hover:border-gray-600 hover:bg-gray-50/80 dark:hover:bg-white/10"
  >
    <span
      class="w-3.5 h-3.5 rounded-full shrink-0 border border-gray-200/70 dark:border-gray-700/70"
      :style="{ backgroundColor: tag.color }"
    />
    <span class="text-xs font-medium truncate max-w-44" :title="tag.name">{{ tag.name }}</span>
  </div>
</template>

<script setup lang="ts">
import type { TagDto } from "@/api/tag";

defineProps<{
  tag: TagDto;
}>();
</script>

<style scoped></style>
