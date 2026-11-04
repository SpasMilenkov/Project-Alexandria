<template>
  <figure class="px-6 py-4">
    <div class="relative mx-auto max-w-80">
      <svg viewBox="0 0 360 360" class="block w-full" aria-hidden="true">
        <circle cx="180" cy="180" r="82" fill="none" stroke="var(--wrapped-line)" />

        <line
          v-for="(mark, index) in tally.marks"
          :key="index"
          v-bind="{ x1: mark.x1, y1: mark.y1, x2: mark.x2, y2: mark.y2 }"
          :stroke="mark.value === tally.unit ? 'var(--wrapped-purple)' : 'var(--wrapped-rose)'"
          stroke-width="4"
          stroke-linecap="round"
        />
      </svg>

      <div class="absolute inset-0 flex flex-col items-center justify-center">
        <span class="text-5xl font-black tabular-nums text-gray-900 dark:text-gray-100">{{
          count.toLocaleString()
        }}</span>

        <span class="mt-2 text-xs tracking-widest text-gray-600 uppercase dark:text-gray-400"
          >times on repeat</span
        >
      </div>
    </div>

    <figcaption class="text-center text-xs text-gray-600 dark:text-gray-400">
      <span v-if="tally.unit === 1">One mark for every play.</span>
      <span v-else
        >Each full mark is {{ tally.unit }} plays.<span v-if="tally.remainder">
          The final mark is {{ tally.remainder }}.</span
        ></span
      >
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed } from "vue";

import { replayMarks } from "@/utils/wrapped-story.utils";

const props = defineProps<{ count: number }>();

const tally = computed(() => replayMarks(props.count));
</script>
