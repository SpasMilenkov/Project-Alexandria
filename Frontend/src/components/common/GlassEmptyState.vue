<script setup lang="ts">
const {
  icon,
  title,
  description = undefined,
} = defineProps<{
  icon: string;
  title: string;
  description?: string;
}>();
</script>

<template>
  <section
    class="frosted-glass glass-surface-strong flex w-full max-w-md flex-col items-center gap-3 rounded-3xl border border-gray-200/70 p-6 text-center shadow-xl sm:p-8 dark:border-gray-700/70"
  >
    <span
      class="flex size-12 items-center justify-center rounded-2xl bg-gray-500/15 text-gray-900 dark:text-gray-100"
      aria-hidden="true"
    >
      <UIcon :name="icon" class="size-6" />
    </span>

    <h2 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
      {{ title }}
    </h2>

    <p
      v-if="description || $slots.description"
      class="text-sm leading-relaxed text-gray-600 dark:text-gray-400"
    >
      <slot name="description">{{ description }}</slot>
    </p>

    <div v-if="$slots.default" class="mt-2 flex flex-wrap justify-center gap-2">
      <slot />
    </div>
  </section>
</template>
