<template>
  <figure class="px-6 py-6">
    <div
      class="mb-4 flex flex-wrap items-center justify-between gap-4 text-xs text-gray-600 dark:text-gray-400"
    >
      <span>{{ dateLabel(days[0]?.date) }} – {{ dateLabel(days[days.length - 1]?.date) }}</span>

      <button
        v-if="facts.series.length > 31"
        type="button"
        class="rounded px-2 py-2 underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-primary"
        :aria-pressed="whole"
        @click="whole = !whole"
      >
        {{ whole ? "Around the peak" : "Whole period" }}
      </button>
    </div>

    <div
      ref="wave"
      class="relative flex h-52 items-center overflow-x-auto"
      role="group"
      aria-label="Daily listening waveform. Use left and right arrows to inspect dates."
    >
      <div
        class="pointer-events-none absolute inset-x-0 top-1/2 h-px bg-gray-300 dark:bg-gray-600"
      />

      <button
        v-for="(day, index) in days"
        :key="day.date"
        type="button"
        :data-date="day.date"
        :tabindex="focusIndex === index ? 0 : -1"
        :aria-label="describe(day)"
        :title="describe(day)"
        class="relative flex h-full min-w-1 flex-1 items-center justify-center rounded focus-visible:outline-2 focus-visible:outline-primary"
        @click="selected = day"
        @focus="selected = day"
        @keydown="move($event, index)"
      >
        <span
          class="w-3/4 rounded-full"
          :style="{
            height: `${(day.seconds / max) * 92}%`,
            backgroundColor:
              day.date === facts.from ? 'var(--wrapped-rose)' : 'var(--wrapped-blue)',
          }"
        />
      </button>
    </div>

    <figcaption class="mt-4 text-xs text-gray-600 dark:text-gray-400" aria-live="polite">
      {{ selected ? describe(selected) : storyNote }}
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedCardFacts, WrappedPeriodPoint } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { wrappedStoryNote } from "@/utils/wrapped-copy.utils";
import { dateLabel, waveformDays } from "@/utils/wrapped-story.utils";

const props = defineProps<{ facts: WrappedCardFacts }>();
const whole = ref(false);
const selected = ref<WrappedPeriodPoint | null>(null);
const wave = ref<HTMLElement | null>(null);

const storyNote = computed(() =>
  wrappedStoryNote({ type: WrappedCardType.BusiestDay, facts: props.facts }),
);

watch(
  () => props.facts,
  () => {
    selected.value = null;
    whole.value = false;
  },
);

const days = computed(() => waveformDays(props.facts.series, props.facts.from, whole.value));

const max = computed(() => Math.max(1, ...props.facts.series.map((day) => day.seconds)));

const focusIndex = computed(() =>
  Math.max(
    0,
    days.value.findIndex((day) => day.date === (selected.value?.date ?? props.facts.from)),
  ),
);

const describe = (day: WrappedPeriodPoint) =>
  `${dateLabel(day.date)} · ${formatWrappedDuration(day.seconds)} · ${day.plays} plays${day.date === props.facts.from ? " · your peak day" : ""}`;

const move = (event: KeyboardEvent, index: number) => {
  if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;

  event.preventDefault();

  const shift = event.key === "ArrowLeft" ? -1 : 1;
  const next = days.value[index + shift];

  if (next)
    wave.value?.querySelector<HTMLButtonElement>(`button[data-date="${next.date}"]`)?.focus();
};
</script>
