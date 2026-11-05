<template>
  <section class="flex flex-col gap-3" :aria-labelledby="headingId">
    <div class="flex min-h-7 items-center justify-between gap-2">
      <h3 :id="headingId" class="text-sm font-semibold text-gray-900 dark:text-gray-100">
        {{ title }}
      </h3>

      <div v-if="$slots.actions" class="flex items-center gap-1">
        <slot name="actions" />
      </div>
    </div>

    <slot />
  </section>
</template>

<script setup lang="ts">
import { useId } from "vue";

defineProps<{
  title: string;
}>();

const headingId = useId();
</script>
