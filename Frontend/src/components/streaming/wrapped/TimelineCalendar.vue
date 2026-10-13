<template>
  <div
    ref="root"
    class="grid grid-cols-1 gap-6 text-gray-700 sm:grid-cols-2 xl:grid-cols-3 dark:text-gray-300"
  >
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
        <template v-for="(cell, offset) in month.cells.slice((row - 1) * 7, row * 7)" :key="offset">
          <button
            v-if="cell?.inRange"
            type="button"
            role="gridcell"
            :data-day="cell.date"
            :tabindex="focusDate(month.cells) === cell.date ? 0 : -1"
            :aria-label="describe(cell)"
            :aria-selected="modelValue === cell.date"
            :title="describe(cell)"
            class="aspect-square min-w-0 rounded-sm border border-gray-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-600"
            :class="{ 'ring-2 ring-primary': modelValue === cell.date }"
            :style="{ backgroundColor: cellColor(cell) }"
            @click="select(cell.date)"
            @focus="select(cell.date)"
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
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

import type { TimelineDayPoint } from "@/api/stats";

import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { type CalendarCell, calendarMonths, dateLabel } from "@/utils/wrapped-story.utils";

const { days, from, to, modelValue } = defineProps<{
  days: TimelineDayPoint[];
  from: string;
  to: string;
  modelValue: string | null;
}>();

const emit = defineEmits<{ "update:modelValue": [date: string] }>();

const root = ref<HTMLElement | null>(null);
const weekdays = ["M", "T", "W", "T", "F", "S", "S"];

const months = computed(() => calendarMonths(days, from, to));

const max = computed(() => Math.max(1, ...days.map((day) => day.seconds)));

const cellColor = (cell: CalendarCell) => {
  if (cell.seconds <= 0) return "transparent";

  return `color-mix(in srgb, currentColor ${20 + Math.sqrt(cell.seconds / max.value) * 50}%, transparent)`;
};

const describe = (cell: CalendarCell) =>
  `${dateLabel(cell.date)} · ${formatWrappedDuration(cell.seconds)} · ${cell.plays} plays`;

const focusDate = (cells: (CalendarCell | null)[]) =>
  cells.find((cell) => cell?.date === modelValue)?.date ??
  cells.find((cell) => cell?.inRange)?.date;

const select = (date: string) => emit("update:modelValue", date);

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
