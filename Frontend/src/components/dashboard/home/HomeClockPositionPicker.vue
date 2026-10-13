<script setup lang="ts">
import type { HomeHorizontalAlignment, HomeVerticalAlignment } from "@/enums/home-widget";

import { HOME_CLOCK_ANCHORS } from "@/utils/home-widget-options";

const { horizontal, vertical, target } = defineProps<{
  horizontal: HomeHorizontalAlignment;
  vertical: HomeVerticalAlignment;
  target: "clock" | "text";
}>();

const emit = defineEmits<{
  change: [horizontal: HomeHorizontalAlignment, vertical: HomeVerticalAlignment];
}>();
</script>

<template>
  <div
    class="grid grid-cols-3 gap-2 rounded-xl border border-gray-200/70 p-2 dark:border-gray-700/70"
    role="group"
    :aria-label="target === 'clock' ? 'Clock position' : 'Text position'"
  >
    <button
      v-for="anchor in HOME_CLOCK_ANCHORS"
      :key="anchor.label"
      type="button"
      :aria-label="`Position ${target} at ${anchor.label}`"
      :title="anchor.label"
      :aria-pressed="horizontal === anchor.horizontal && vertical === anchor.vertical"
      class="flex h-10 items-center justify-center rounded-lg border border-transparent hover:border-gray-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:hover:border-gray-700"
      :class="{
        'ring-1 ring-primary': horizontal === anchor.horizontal && vertical === anchor.vertical,
      }"
      @click="emit('change', anchor.horizontal, anchor.vertical)"
    >
      <UIcon
        v-if="horizontal === anchor.horizontal && vertical === anchor.vertical"
        :name="target === 'clock' ? 'i-heroicons-clock' : 'i-heroicons-document-text'"
        class="size-5 text-primary"
        aria-hidden="true"
      />
      <span v-else class="size-2 rounded-full bg-gray-500 dark:bg-gray-400" aria-hidden="true" />
    </button>
  </div>
</template>
