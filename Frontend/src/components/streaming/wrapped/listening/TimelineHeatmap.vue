<template>
  <div ref="scroller" class="relative overflow-x-auto pb-2">
    <div
      ref="root"
      role="grid"
      :aria-label="`Listening days in ${year}`"
      class="grid gap-[3px]"
      :style="gridStyle"
    >
      <span
        v-for="week in labelledWeeks"
        :key="`month-${week.key}`"
        aria-hidden="true"
        class="truncate pb-1 text-[10px] text-gray-600 dark:text-gray-400"
        :style="{ gridColumn: week.column, gridRow: 1 }"
        >{{ week.month }}</span
      >

      <span
        v-for="(day, index) in weekdays"
        :key="`weekday-${index}`"
        aria-hidden="true"
        class="pr-1 text-[10px] leading-none text-gray-600 dark:text-gray-400"
        :style="{ gridColumn: 1, gridRow: index + 2, alignSelf: 'center' }"
        >{{ day }}</span
      >

      <template v-for="(week, column) in weeks" :key="week.key">
        <template v-for="(cell, row) in week.cells" :key="`${week.key}-${row}`">
          <button
            v-if="cell && !cell.future"
            type="button"
            role="gridcell"
            :data-day="cell.date"
            :tabindex="focusDate === cell.date ? 0 : -1"
            :aria-label="describe(cell)"
            :aria-selected="modelValue === cell.date"
            :title="describe(cell)"
            class="aspect-square min-w-0 rounded-[3px] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primary"
            :class="{
              'bg-gray-500/15': cell.seconds <= 0,
              'ring-2 ring-primary': modelValue === cell.date,
            }"
            :style="cellStyle(cell, column, row)"
            @click="select(cell.date)"
            @focus="select(cell.date)"
            @keydown="move($event, cell.date)"
          />
          <span
            v-else-if="cell"
            aria-hidden="true"
            class="aspect-square rounded-[3px] border border-dashed border-gray-300 dark:border-gray-700"
            :style="{ gridColumn: column + 2, gridRow: row + 2 }"
          />
        </template>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";

import type { TimelineDayPoint } from "@/api/stats";

import { formatWrappedDuration } from "@/features/wrapped/art";
import { type HeatmapCell, heatLevel, heatmapWeeks } from "@/features/wrapped/listening/year";
import { dateLabel } from "@/features/wrapped/story";
import { utcDayKey } from "@/utils/date-formatters";

const { days, year, today, modelValue } = defineProps<{
  days: TimelineDayPoint[];
  year: number;
  today: number;
  modelValue: string | null;
}>();

const emit = defineEmits<{ "update:modelValue": [date: string] }>();

const LEVEL_MIX = [0, 30, 50, 75, 100];
const DAY_MS = 86_400_000;

const scroller = ref<HTMLElement | null>(null);
const root = ref<HTMLElement | null>(null);
const weekdays = ["Mon", "", "Wed", "", "Fri", "", ""];

const weeks = computed(() => heatmapWeeks(days, year, today));

const labelledWeeks = computed(() =>
  weeks.value.flatMap((week, index) =>
    week.month ? [{ key: week.key, month: week.month, column: `${index + 2} / span 3` }] : [],
  ),
);

const gridStyle = computed(() => ({
  gridTemplateColumns: `auto repeat(${weeks.value.length}, minmax(11px, 1fr))`,
  minWidth: `${weeks.value.length * 14 + 32}px`,
}));

const max = computed(() => Math.max(0, ...days.map((day) => day.seconds)));

const focusDate = computed(() => {
  const cells = weeks.value.flatMap((week) => week.cells);
  const past = cells.filter((cell): cell is HeatmapCell => Boolean(cell && !cell.future));

  return past.find((cell) => cell.date === modelValue)?.date ?? past.at(-1)?.date;
});

const cellStyle = (cell: HeatmapCell, column: number, row: number) => {
  const position = { gridColumn: column + 2, gridRow: row + 2 };
  const level = heatLevel(cell.seconds, max.value);

  if (level === 0) return position;

  return {
    ...position,
    backgroundColor: `color-mix(in srgb, var(--wrapped-blue) ${LEVEL_MIX[level]}%, transparent)`,
  };
};

const describe = (cell: HeatmapCell) =>
  `${dateLabel(cell.date)} · ${formatWrappedDuration(cell.seconds)} · ${cell.plays} plays`;

const select = (date: string) => emit("update:modelValue", date);

const KEY_SHIFTS: Record<string, number> = {
  ArrowLeft: -7,
  ArrowRight: 7,
  ArrowUp: -1,
  ArrowDown: 1,
};

const move = (event: KeyboardEvent, date: string) => {
  const shift = KEY_SHIFTS[event.key];

  if (shift === undefined) return;

  event.preventDefault();

  const target = utcDayKey(Date.parse(date) + shift * DAY_MS);

  root.value?.querySelector<HTMLButtonElement>(`button[data-day="${target}"]`)?.focus();
};

// Narrow screens scroll the year sideways; start on the latest listening rather than January.
const revealLatest = async () => {
  await nextTick();

  const container = scroller.value;
  const latest = focusDate.value;

  if (!container || !latest || container.scrollWidth <= container.clientWidth) return;

  const cell = container.querySelector<HTMLElement>(`[data-day="${latest}"]`);

  if (!cell) return;

  container.scrollLeft = cell.offsetLeft - container.clientWidth + cell.offsetWidth * 4;
};

onMounted(revealLatest);

watch(() => year, revealLatest);
</script>
