<template>
  <div>
    <div
      class="flex items-center gap-3 text-xs tracking-[0.16em] uppercase"
      :style="{ color: ink }"
    >
      <svg
        viewBox="0 0 40 40"
        class="h-10 w-10 shrink-0"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linejoin="round"
        stroke-linecap="round"
        aria-hidden="true"
      >
        <circle cx="20" cy="20" r="16" />
        <path d="M 8 28.5 L 18 11 L 23 23 L 26 16 L 32 28.5 M 8.4 29 H 31.6" />
        <circle cx="28" cy="11" r="2" fill="currentColor" stroke="none" />
      </svg>

      <span>{{ category }} · a milestone in time</span>
    </div>

    <p class="mt-4 text-xs font-medium tracking-wide text-gray-600 uppercase dark:text-gray-400">
      {{ relationship }}
    </p>

    <h3 class="mt-2 text-xl leading-tight font-bold text-gray-900 dark:text-gray-100">
      {{ selected.name }}
    </h3>

    <p class="mt-3 text-sm" :style="{ color: ink }">{{ selected.durationLabel }}</p>

    <blockquote
      v-if="selected.description"
      class="mt-4 text-sm leading-relaxed text-gray-600 dark:text-gray-400"
    >
      <p class="mb-2 text-xs font-medium tracking-wide uppercase">At this milestone</p>

      <p>{{ selected.description }}</p>
    </blockquote>

    <p v-if="!selected.exact" class="mt-2 text-xs text-gray-600 dark:text-gray-400">
      An approximate duration; schedules and editions can vary.
    </p>

    <div v-if="choices.length > 1" class="mt-4 flex flex-wrap gap-2">
      <button
        v-for="(choice, index) in choices"
        :key="choice.key"
        type="button"
        :aria-pressed="active === index"
        class="rounded-lg border border-gray-300 px-3 py-2 text-xs text-gray-700 focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-600 dark:text-gray-300"
        :class="{ 'font-bold': active === index }"
        @click="active = index"
      >
        {{ index === 0 ? "Your milestone" : `Perspective ${index + 1}` }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedComparison } from "@/api/stats";

import { WrappedDurationRelationship } from "@/enums/wrapped-story";

const props = defineProps<{
  comparison: WrappedComparison;
  alternatives?: WrappedComparison[];
  ink: string;
}>();

const active = ref(0);

watch(
  () => props.comparison.key,
  () => {
    active.value = 0;
  },
);

const choices = computed(() => [props.comparison, ...(props.alternatives ?? []).slice(0, 2)]);

const selected = computed(() => choices.value[active.value] ?? props.comparison);

const category = computed(
  () =>
    [
      "Sport",
      "Cinema",
      "History",
      "Cinema",
      "Television",
      "Everyday life",
      "Journeys",
      "Time",
      "Space",
      "Animation",
      "Theatre",
      "Audiobooks",
    ][selected.value.category] ?? "Time",
);

const relationship = computed(() => {
  if (selected.value.relationship === WrappedDurationRelationship.MilestonePassed)
    return "Your listening passed";

  if (selected.value.relationship === WrappedDurationRelationship.WithinRange)
    return "Your listening fits the time of";

  return "About as much time as";
});
</script>
