<template>
  <section
    class="overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <header
      class="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-gray-200/70 px-4 py-3 sm:px-5 dark:border-gray-700/70"
    >
      <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">{{ title }}</h2>
      <span v-if="hint" class="ml-auto text-xs text-gray-600 dark:text-gray-400">{{ hint }}</span>
      <slot name="actions" />
    </header>

    <div :class="{ 'p-4 sm:p-5': !flush }">
      <slot />
    </div>
  </section>
</template>

<script setup lang="ts">
const {
  title,
  hint = null,
  flush = false,
} = defineProps<{
  title: string;
  hint?: string | null;
  // Lists and tables run edge to edge; charts and summaries get padding.
  flush?: boolean;
}>();
</script>
