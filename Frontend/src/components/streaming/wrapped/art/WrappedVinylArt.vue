<template>
  <figure class="px-6 pt-6 pb-4">
    <svg
      viewBox="0 0 360 360"
      class="mx-auto block w-full max-w-96"
      role="img"
      :aria-label="`Your record, from ${dateLabel(facts.from)} to ${dateLabel(facts.to)}`"
    >
      <circle cx="180" cy="180" r="158" fill="var(--wrapped-track)" />
      <circle cx="180" cy="180" r="152" fill="var(--wrapped-cutout)" />

      <path
        v-for="segment in groove.segments"
        :key="segment.date"
        :d="segment.path"
        fill="none"
        stroke="var(--wrapped-purple)"
        :stroke-width="segment.width"
        stroke-linecap="round"
      />

      <circle cx="180" cy="180" r="28" fill="var(--wrapped-clay)" />
      <circle cx="180" cy="180" r="4" fill="var(--wrapped-cutout)" />

      <circle
        :cx="groove.first.x"
        :cy="groove.first.y"
        r="6"
        fill="var(--wrapped-blue)"
        stroke="var(--wrapped-cutout)"
        stroke-width="2"
      />

      <path
        v-if="!groove.single"
        :d="`M ${groove.last.x} ${groove.last.y - 7} L ${groove.last.x + 7} ${groove.last.y} L ${groove.last.x} ${groove.last.y + 7} L ${groove.last.x - 7} ${groove.last.y} Z`"
        fill="var(--wrapped-rose)"
        stroke="var(--wrapped-cutout)"
        stroke-width="2"
      />
    </svg>

    <figcaption class="text-center text-xs text-gray-600 dark:text-gray-400">
      <p>
        {{
          groove.single
            ? "One needle drop. Your opening note."
            : "First drop to latest lift. The groove deepens where you listened more."
        }}
      </p>

      <p class="mt-2">
        {{ dateLabel(facts.from) }}<span v-if="!groove.single"> → {{ dateLabel(facts.to) }}</span>
      </p>
    </figcaption>

    <div v-if="months.length > 1" class="mt-4 flex flex-wrap justify-center gap-2">
      <button
        v-for="month in months"
        :key="month.date"
        type="button"
        class="rounded px-2 py-2 text-xs text-gray-600 underline decoration-gray-400 underline-offset-4 focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400"
        @click="selected = month"
      >
        {{ month.label }}
      </button>
    </div>

    <p
      v-if="selected"
      class="mt-2 text-center text-xs text-gray-600 dark:text-gray-400"
      aria-live="polite"
    >
      {{ selected.label }} · {{ formatWrappedDuration(selected.seconds) }}
    </p>
  </figure>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedCardFacts } from "@/api/stats";

import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { dateLabel, vinylGroove } from "@/utils/wrapped-story.utils";

const props = defineProps<{ facts: WrappedCardFacts; from: string; to: string }>();

const groove = computed(() => vinylGroove(props.facts, props.from, props.to));

const selected = ref<{ label: string; seconds: number } | null>(null);

watch(
  () => props.facts,
  () => {
    selected.value = null;
  },
);

const months = computed(() => {
  const totals = new Map<string, number>();

  props.facts.series.forEach((day) => {
    const key = day.date.slice(0, 7);

    totals.set(key, (totals.get(key) ?? 0) + day.seconds);
  });

  return [...totals].map(([date, seconds]) => ({
    date,
    seconds,

    label: new Intl.DateTimeFormat(undefined, { month: "short", timeZone: "UTC" }).format(
      new Date(`${date}-01`),
    ),
  }));
});
</script>
