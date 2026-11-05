<template>
  <section
    class="flex flex-col gap-3 lg:grid lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-6 lg:py-6 lg:first:pt-0 lg:last:pb-0"
    :aria-labelledby="headingId"
  >
    <div class="flex flex-col gap-1 lg:pt-1.5">
      <div class="flex items-center gap-2">
        <span
          aria-hidden="true"
          class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-gray-500/10 text-[11px] font-semibold tabular-nums text-gray-600 dark:text-gray-400"
        >
          {{ step }}
        </span>

        <h3 :id="headingId" class="text-sm font-semibold text-gray-900 dark:text-gray-100">
          {{ title }}
        </h3>
      </div>

      <p v-if="description" class="hidden pl-7 text-xs text-gray-600 lg:block dark:text-gray-400">
        {{ description }}
      </p>
    </div>

    <div class="flex min-w-0 flex-col gap-3 sm:pl-7 lg:pl-0">
      <slot />
    </div>
  </section>
</template>

<script setup lang="ts">
import { useId } from "vue";

const { description = undefined } = defineProps<{
  step: number;
  title: string;
  description?: string;
}>();

const headingId = useId();
</script>
