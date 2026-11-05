<template>
  <component
    :is="to ? RouterLink : 'div'"
    :to="to ?? undefined"
    class="group flex h-full flex-col gap-4 rounded-[14px] border p-4 frosted-glass glass-surface transition-colors duration-150 sm:p-5"
    :class="stateClass"
  >
    <div class="flex items-start gap-3">
      <span
        class="grid h-10 w-10 shrink-0 place-items-center rounded-lg border border-gray-200/70 text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
      >
        <UIcon :name="icon" class="h-5 w-5" />
      </span>

      <div class="min-w-0 flex-1">
        <h3 class="text-base font-semibold text-gray-900 dark:text-gray-100">{{ title }}</h3>
        <p class="mt-0.5 text-sm text-gray-600 dark:text-gray-400">{{ description }}</p>
      </div>

      <div class="flex shrink-0 items-center gap-2">
        <slot name="meta" />
        <UIcon
          v-if="to"
          name="i-lucide-chevron-right"
          class="h-4 w-4 text-gray-400 transition-colors duration-150 group-hover:text-gray-600 dark:text-gray-500 dark:group-hover:text-gray-300"
        />
      </div>
    </div>

    <div v-if="$slots.default" class="mt-auto">
      <slot />
    </div>
  </component>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";

const {
  to = null,
  icon,
  title,
  description,
} = defineProps<{
  to?: string | null;
  icon: string;
  title: string;
  description: string;
}>();

const stateClass = computed(() => {
  if (!to) return "border-dashed border-gray-300/70 dark:border-gray-600/70";

  return "border-gray-200/70 hover:border-gray-300 hover:bg-gray-900/5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-700/70 dark:hover:border-gray-600 dark:hover:bg-white/5";
});
</script>
