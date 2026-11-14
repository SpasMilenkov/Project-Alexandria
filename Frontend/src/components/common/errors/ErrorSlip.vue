<script setup lang="ts">
const { title, stamp = undefined } = defineProps<{ title: string; stamp?: string }>();

defineSlots<{ default: () => unknown; footer?: () => unknown }>();
</script>

<template>
  <aside
    class="frosted-glass glass-surface flex flex-col rounded-3xl border border-gray-200/70 p-6 sm:p-8 dark:border-gray-700/70"
  >
    <header
      class="flex items-baseline justify-between gap-4 border-b-4 border-double border-gray-300/80 pb-3 dark:border-gray-600/80"
    >
      <h2 class="text-sm font-semibold tracking-[0.2em] text-gray-900 uppercase dark:text-gray-100">
        {{ title }}
      </h2>

      <span v-if="stamp" class="font-mono text-xs text-gray-600 dark:text-gray-400">
        {{ stamp }}
      </span>
    </header>

    <dl class="divide-y divide-dashed divide-gray-300/80 dark:divide-gray-600/80">
      <slot />
    </dl>

    <div v-if="$slots.footer" class="mt-6">
      <slot name="footer" />
    </div>
  </aside>
</template>
