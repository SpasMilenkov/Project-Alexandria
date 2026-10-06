<template>
  <figure ref="root" class="px-6 py-6">
    <div class="grid grid-cols-2 gap-6 lg:grid-cols-4">
      <div v-for="month in months" :key="month.key" role="grid" :aria-label="month.label">
        <p class="mb-3 text-xs font-semibold text-gray-900 dark:text-gray-100">{{ month.label }}</p>

        <div class="grid grid-cols-7 gap-1" role="row">
          <span
            v-for="(day, i) in weekdays"
            :key="i"
            role="columnheader"
            class="pb-1 text-center text-[10px] text-gray-600 dark:text-gray-400"
            >{{ day }}</span
          >
        </div>

        <div v-for="row in 6" :key="row" class="mb-1 grid grid-cols-7 gap-1" role="row">
          <template
            v-for="(cell, offset) in month.cells.slice((row - 1) * 7, row * 7)"
            :key="offset"
          >
            <button
              v-if="cell?.inRange"
              type="button"
              role="gridcell"
              :data-day="cell.date"
              :tabindex="focusDate(month.cells) === cell.date ? 0 : -1"
              :aria-label="describe(cell)"
              :aria-selected="selected?.date === cell.date"
              :title="describe(cell)"
              class="aspect-square min-w-0 rounded-sm border border-gray-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-600"
              :class="{ 'ring-2 ring-gray-900 dark:ring-gray-100': cell.streak }"
              :style="{ backgroundColor: cellColor(cell) }"
              @click="selected = cell"
              @focus="selected = cell"
              @keydown="move($event, cell.date, month.cells)"
            />
            <span
              v-else
              role="gridcell"
              class="aspect-square rounded-sm"
              :class="{ 'border border-dashed border-gray-200 dark:border-gray-700': cell?.future }"
            />
          </template>
        </div>
      </div>
    </div>

    <figcaption class="mt-6 text-xs leading-relaxed text-gray-600 dark:text-gray-400">
      <p aria-live="polite">
        {{ selected ? describe(selected) : "Choose a date to revisit your listening." }}
      </p>

      <p class="mt-2">
        Deeper color, more music. The outlined dates show your longest listening streak.
      </p>
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedCardFacts } from "@/api/stats";

import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { calendarMonths, dateLabel, type CalendarCell } from "@/utils/wrapped-story.utils";

const props = defineProps<{ facts: WrappedCardFacts; from: string; to: string }>();
const root = ref<HTMLElement | null>(null);
const selected = ref<CalendarCell | null>(null);

watch(
  () => props.facts,
  () => {
    selected.value = null;
  },
);

const weekdays = ["M", "T", "W", "T", "F", "S", "S"];

const months = computed(() =>
  calendarMonths(props.facts.series, props.from, props.to, props.facts.from, props.facts.to),
);

const max = computed(() => Math.max(1, ...props.facts.series.map((day) => day.seconds)));

const cellColor = (cell: CalendarCell) => {
  if (cell.seconds <= 0) return "transparent";

  return `color-mix(in srgb, var(--wrapped-blue) ${25 + Math.sqrt(cell.seconds / max.value) * 75}%, var(--wrapped-track))`;
};

const describe = (cell: CalendarCell) =>
  `${dateLabel(cell.date)} · ${formatWrappedDuration(cell.seconds)} · ${cell.plays} plays${cell.streak ? " · longest streak" : ""}`;

const focusDate = (cells: (CalendarCell | null)[]) =>
  cells.find((cell) => cell?.date === selected.value?.date)?.date ??
  cells.find((cell) => cell?.inRange)?.date;

const move = (event: KeyboardEvent, date: string, cells: (CalendarCell | null)[]) => {
  const shifts: Record<string, number> = {
    ArrowLeft: -1,
    ArrowRight: 1,
    ArrowUp: -7,
    ArrowDown: 7,
  };

  const shift = shifts[event.key];

  if (shift === undefined) return;

  event.preventDefault();

  const index = cells.findIndex((cell) => cell?.date === date);
  let next = index + shift;

  while (next >= 0 && next < cells.length) {
    const cell = cells[next];

    if (cell?.inRange) {
      root.value?.querySelector<HTMLButtonElement>(`button[data-day="${cell.date}"]`)?.focus();

      return;
    }

    next += shift;
  }
};
</script>
