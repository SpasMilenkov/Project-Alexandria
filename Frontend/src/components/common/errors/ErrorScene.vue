<script setup lang="ts">
const { eyebrow, title } = defineProps<{ eyebrow: string; title: string }>();

defineSlots<{
  actions?: () => unknown;
  aside?: () => unknown;
  description?: () => unknown;
  mark?: () => unknown;
}>();
</script>

<template>
  <section class="flex min-h-full w-full p-4 sm:p-6 lg:p-8">
    <div class="m-auto grid w-full gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:gap-6">
      <div
        class="frosted-glass glass-surface flex flex-col justify-center gap-6 rounded-3xl border border-gray-200/70 p-6 sm:p-10 dark:border-gray-700/70"
      >
        <p
          class="text-xs font-semibold tracking-[0.2em] text-gray-600 uppercase dark:text-gray-400"
        >
          {{ eyebrow }}
        </p>

        <div
          class="flex items-center gap-[0.06em] text-[5.5rem] leading-none font-black tracking-tighter text-gray-900 sm:text-[8rem] xl:text-[10rem] dark:text-gray-100"
          aria-hidden="true"
        >
          <slot name="mark" />
        </div>

        <div class="flex flex-col gap-3">
          <h1
            class="text-2xl font-semibold tracking-tight text-gray-900 sm:text-3xl dark:text-gray-100"
          >
            {{ title }}
          </h1>

          <p class="max-w-xl text-base leading-relaxed text-gray-600 dark:text-gray-400">
            <slot name="description" />
          </p>
        </div>

        <div class="flex flex-wrap gap-2">
          <slot name="actions" />
        </div>
      </div>

      <slot name="aside" />
    </div>
  </section>
</template>
