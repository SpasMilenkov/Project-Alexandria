<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import { OperationType } from "@/api/activity";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { type CalendarDay, useActivityCalendar } from "@/composables/useActivityCalendar";
import { activitySummary } from "@/queries/activities";
import { formatCount } from "@/utils/count";
import { formatDayMonth, formatFullDate } from "@/utils/date-formatters";

const isMobile = useMediaQuery("(max-width: 767px)");

const currentYear = new Date().getFullYear();
const currentMonth = new Date().getMonth();
const MIN_YEAR = currentYear - 4;

const selectedYear = ref(currentYear);
const selectedMonth = ref(currentMonth);
const selectedDay = ref<CalendarDay | null>(null);

// Changing the year snaps the month to "now" in the current year, otherwise to January.
watch(selectedYear, (yr) => {
  selectedMonth.value = yr === currentYear ? currentMonth : 0;
  selectedDay.value = null;
});

const canGoBack = computed(() => selectedYear.value > MIN_YEAR);
const canGoForward = computed(() => selectedYear.value < currentYear);

const prevYear = () => {
  if (canGoBack.value) selectedYear.value--;
};

const nextYear = () => {
  if (canGoForward.value) selectedYear.value++;
};

const canGoBackMonth = computed(() => selectedMonth.value > 0 || selectedYear.value > MIN_YEAR);
const canGoForwardMonth = computed(
  () => !(selectedYear.value === currentYear && selectedMonth.value >= currentMonth),
);

const prevMonth = () => {
  if (!canGoBackMonth.value) return;

  if (selectedMonth.value === 0) {
    selectedYear.value--;
    selectedMonth.value = 11;
  } else {
    selectedMonth.value--;
  }

  selectedDay.value = null;
};

const nextMonth = () => {
  if (!canGoForwardMonth.value) return;

  if (selectedMonth.value === 11) {
    selectedYear.value++;
    selectedMonth.value = 0;
  } else {
    selectedMonth.value++;
  }

  selectedDay.value = null;
};

const mobileMonthLabel = computed(() =>
  new Date(selectedYear.value, selectedMonth.value).toLocaleDateString(undefined, {
    month: "long",
    year: "numeric",
  }),
);

const selectedOperation = ref<OperationType | null>(null);

const FILTERS: { label: string; value: OperationType | null }[] = [
  { label: "All", value: null },
  { label: "Create", value: OperationType.Create },
  { label: "Read", value: OperationType.Read },
  { label: "Update", value: OperationType.Update },
  { label: "Delete", value: OperationType.Delete },
];

const setFilter = (op: OperationType | null) => {
  selectedOperation.value = op;
  selectedDay.value = null;
};

// Always the full year; the mobile month view slices it client-side.
// oxlint-disable-next-line sort-keys
const { data, isPending, error } = useQuery(() =>
  activitySummary({
    startDate: new Date(Date.UTC(selectedYear.value, 0, 1)),
    endDate: new Date(Date.UTC(selectedYear.value + 1, 0, 1)),
  }),
);

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;

  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;

  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;
  return error.value;
});

const { weeks, monthBlocks, totalFiltered } = useActivityCalendar(
  data,
  selectedYear,
  selectedOperation,
);

// Column 1 holds the weekday labels; each month block is followed by a spacer track.
const desktopMonths = computed(() => {
  let nextColumn = 2;

  return monthBlocks.value.map((block) => {
    const startColumn = nextColumn;
    nextColumn += block.weeks.length + 1;

    return { ...block, startColumn };
  });
});

const MONTH_SPACER = "0.25rem";

// Week columns fill the card up to a comfortable cell size; narrow widths scroll horizontally.
const heatmapStyle = computed(() => {
  const monthTracks = desktopMonths.value.map(
    (month) => `repeat(${month.weeks.length}, minmax(0, 1.5rem))`,
  );

  return { gridTemplateColumns: `max-content ${monthTracks.join(` ${MONTH_SPACER} `)}` };
});

// Weeks straddling a month boundary are kept so the grid stays aligned; foreign days render dimmed.
const mobileWeeks = computed(() =>
  weeks.value.filter((week) =>
    week.some((day) => !day.isPadding && day.date?.getMonth() === selectedMonth.value),
  ),
);

const isForeignMonthCell = (day: CalendarDay): boolean =>
  !day.isPadding && day.date?.getMonth() !== selectedMonth.value;

const mobileMonthTotal = computed(() =>
  mobileWeeks.value
    .flat()
    .filter((d) => !d.isPadding && d.date?.getMonth() === selectedMonth.value)
    .reduce((sum, d) => sum + d.count, 0),
);

const displayTotal = computed(() => {
  if (isMobile.value) return mobileMonthTotal.value;
  return totalFiltered.value;
});

const isSelectable = (day: CalendarDay): boolean => !day.isPadding && day.count > 0;

const isSelected = (day: CalendarDay): boolean =>
  !day.isPadding && selectedDay.value?.dayOfYear === day.dayOfYear;

const selectDay = (day: CalendarDay) => {
  if (!isSelectable(day)) return;
  selectedDay.value = isSelected(day) ? null : day;
};

// Hover tooltip is desktop only so touch taps never leave a phantom tooltip behind.
const tooltipVisible = ref(false);
const tooltipX = ref(0);
const tooltipY = ref(0);
const tooltipDay = ref<CalendarDay | null>(null);

const positionTooltip = (event: MouseEvent) => {
  tooltipX.value = event.clientX;
  tooltipY.value = event.clientY;
};

const onCellEnter = (event: MouseEvent, day: CalendarDay) => {
  if (isMobile.value || day.isPadding) return;

  tooltipDay.value = day;
  tooltipVisible.value = true;
  positionTooltip(event);
};

const onCellMove = (event: MouseEvent) => {
  if (tooltipVisible.value) positionTooltip(event);
};

const onCellLeave = () => {
  tooltipVisible.value = false;
  tooltipDay.value = null;
};

const tooltipText = computed(() => {
  const day = tooltipDay.value;
  if (!day?.date) return "";

  const dateStr = formatDayMonth(day.date);
  if (day.count === 0) return `No activity · ${dateStr}`;

  return `${formatCount(day.count, "action")} · ${dateStr}`;
});

const EMPTY_CELL = "bg-gray-400/20 dark:bg-gray-500/20";

const CELL_COLORS: Record<string, readonly string[]> = {
  all: [
    EMPTY_CELL,
    "bg-emerald-200 dark:bg-emerald-900",
    "bg-emerald-400 dark:bg-emerald-700",
    "bg-emerald-600 dark:bg-emerald-500",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Create]: [
    EMPTY_CELL,
    "bg-emerald-200 dark:bg-emerald-900",
    "bg-emerald-400 dark:bg-emerald-700",
    "bg-emerald-600 dark:bg-emerald-500",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Read]: [
    EMPTY_CELL,
    "bg-sky-200 dark:bg-sky-900",
    "bg-sky-400 dark:bg-sky-700",
    "bg-sky-600 dark:bg-sky-500",
    "bg-sky-800 dark:bg-sky-300",
  ],
  [OperationType.Update]: [
    EMPTY_CELL,
    "bg-amber-200 dark:bg-amber-900",
    "bg-amber-400 dark:bg-amber-700",
    "bg-amber-600 dark:bg-amber-500",
    "bg-amber-800 dark:bg-amber-300",
  ],
  [OperationType.Delete]: [
    EMPTY_CELL,
    "bg-rose-200 dark:bg-rose-900",
    "bg-rose-400 dark:bg-rose-700",
    "bg-rose-600 dark:bg-rose-500",
    "bg-rose-800 dark:bg-rose-300",
  ],
};

const cellColorKey = computed(() => {
  if (selectedOperation.value === null) return "all";
  return String(selectedOperation.value);
});

const intensityColor = (intensity: number): string =>
  CELL_COLORS[cellColorKey.value]?.[intensity] ?? EMPTY_CELL;

const cellClass = (day: CalendarDay): string[] => {
  if (day.isPadding) return ["invisible"];

  const classes = [intensityColor(day.intensity)];
  if (isSelectable(day)) classes.push("interactive");
  if (isSelected(day)) classes.push("selected");

  return classes;
};

// Foreign-month days stay visible for orientation but never read as intensity.
const mobileCellClass = (day: CalendarDay): string[] => {
  if (day.isPadding) return ["invisible"];
  if (isForeignMonthCell(day)) return ["bg-gray-400/10 dark:bg-gray-500/10"];

  const classes = [intensityColor(day.intensity)];
  if (isSelectable(day)) classes.push("interactive");
  if (isSelected(day)) classes.push("selected");

  return classes;
};

// Intensity 3 and 4 are dark in light mode and light in dark mode, so the number flips.
const mobileDayNumberClass = (day: CalendarDay): string => {
  if (isForeignMonthCell(day)) return "text-gray-400 dark:text-gray-600";
  if (day.count === 0) return "text-gray-500 dark:text-gray-500";
  if (day.intensity >= 3) return "text-white dark:text-gray-900";
  return "text-gray-800 dark:text-gray-100";
};

const MOBILE_DAY_HEADERS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

const DAY_LABELS = ["Mon", "", "Wed", "", "Fri", "", ""];

const OP_LABELS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]: "Create",
  [OperationType.Read]: "Read",
  [OperationType.Update]: "Update",
  [OperationType.Delete]: "Delete",
  [OperationType.Login]: "Login",
  [OperationType.Logout]: "Logout",
};

type BadgeColor = "success" | "error" | "warning" | "info" | "neutral";

const OP_BADGE_COLORS: Partial<Record<OperationType, BadgeColor>> = {
  [OperationType.Create]: "success",
  [OperationType.Read]: "neutral",
  [OperationType.Update]: "warning",
  [OperationType.Delete]: "error",
  [OperationType.Login]: "info",
};

const dayTitle = (day: CalendarDay): string =>
  day.date ? formatFullDate(day.date, { compact: isMobile.value }) : "";

const breakdownEntries = (
  breakdown: Partial<Record<OperationType, number>>,
): [OperationType, number][] =>
  (Object.entries(breakdown) as [string, number][])
    .map(([k, v]) => [Number(k) as OperationType, v] as [OperationType, number])
    .filter(([, v]) => v > 0);

const hasNoActivity = computed(() => Boolean(data.value) && displayTotal.value === 0);
</script>

<template>
  <section
    class="flex flex-col gap-4 rounded-[14px] border border-gray-200/70 p-4 frosted-glass glass-surface md:p-5 dark:border-gray-700/70"
  >
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div v-if="isMobile" class="flex w-full items-center justify-between gap-2">
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          size="sm"
          class="justify-center"
          :disabled="!canGoBackMonth"
          aria-label="Previous month"
          @click="prevMonth"
        />

        <div class="flex flex-col items-center">
          <span class="text-sm font-semibold select-none">{{ mobileMonthLabel }}</span>
          <span class="text-xs text-gray-500 tabular-nums dark:text-gray-500">
            {{ formatCount(displayTotal, "action") }}
          </span>
        </div>

        <UButton
          icon="i-lucide-chevron-right"
          color="neutral"
          variant="ghost"
          size="sm"
          class="justify-center"
          :disabled="!canGoForwardMonth"
          aria-label="Next month"
          @click="nextMonth"
        />
      </div>

      <div v-else class="flex items-center gap-2">
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          size="xs"
          class="justify-center"
          :disabled="!canGoBack"
          aria-label="Previous year"
          @click="prevYear"
        />
        <span class="w-10 text-center text-sm font-semibold tabular-nums select-none">
          {{ selectedYear }}
        </span>
        <UButton
          icon="i-lucide-chevron-right"
          color="neutral"
          variant="ghost"
          size="xs"
          class="justify-center"
          :disabled="!canGoForward"
          aria-label="Next year"
          @click="nextYear"
        />
        <UBadge color="neutral" variant="subtle" size="sm">
          {{ formatCount(displayTotal, "action") }}
        </UBadge>
      </div>

      <div
        class="flex w-full items-center gap-1 overflow-x-auto md:w-auto"
        role="group"
        aria-label="Filter by operation"
      >
        <UButton
          v-for="filter in FILTERS"
          :key="String(filter.value)"
          :variant="selectedOperation === filter.value ? 'solid' : 'outline'"
          :color="selectedOperation === filter.value ? 'primary' : 'neutral'"
          size="xs"
          class="shrink-0"
          @click="setFilter(filter.value)"
        >
          {{ filter.label }}
        </UButton>
      </div>
    </div>

    <UAlert
      v-if="visibleError"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Failed to load activity"
      :description="visibleError.message"
    />

    <div v-else-if="isPending" class="flex h-48 items-center justify-center">
      <BlockSpinner :size="24" aria-label="Loading activity calendar" class="text-gray-500" />
    </div>

    <div
      v-else-if="hasNoActivity"
      class="flex flex-col items-center justify-center gap-3 py-10 text-center"
    >
      <UIcon name="i-lucide-calendar-x" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
      <p class="text-sm text-gray-600 dark:text-gray-400">
        No activity recorded for {{ isMobile ? mobileMonthLabel : selectedYear }}
      </p>
    </div>

    <div v-else-if="isMobile" class="flex flex-col gap-1.5">
      <div class="grid grid-cols-7 gap-1.5">
        <div
          v-for="label in MOBILE_DAY_HEADERS"
          :key="label"
          class="py-0.5 text-center text-[11px] font-semibold tracking-wide text-gray-600 select-none dark:text-gray-400"
        >
          {{ label }}
        </div>
      </div>

      <div class="grid grid-cols-7 gap-1.5">
        <button
          v-for="(day, idx) in mobileWeeks.flat()"
          :key="idx"
          type="button"
          class="calendar-cell-mobile relative flex aspect-square items-start justify-start rounded-[6px] p-1.5"
          :class="mobileCellClass(day)"
          :disabled="!isSelectable(day) || isForeignMonthCell(day)"
          :aria-label="day.date ? `${dayTitle(day)}: ${formatCount(day.count, 'action')}` : undefined"
          :aria-pressed="isSelected(day)"
          @click="selectDay(day)"
        >
          <span
            v-if="!day.isPadding"
            class="text-[11px] leading-none font-medium select-none"
            :class="mobileDayNumberClass(day)"
          >
            {{ day.date?.getDate() }}
          </span>
        </button>
      </div>
    </div>

    <div v-else class="overflow-x-auto pb-1">
      <div class="grid min-w-[52rem] justify-center gap-1" :style="heatmapStyle">
        <span
          v-for="month in desktopMonths"
          :key="month.label"
          class="pb-0.5 text-[11px] leading-none whitespace-nowrap text-gray-600 dark:text-gray-400"
          :style="{ gridColumn: `${month.startColumn} / span ${month.weeks.length}`, gridRow: 1 }"
        >
          {{ month.label }}
        </span>

        <span
          v-for="(label, di) in DAY_LABELS"
          :key="`day-${di}`"
          class="flex items-center justify-end pr-1.5 text-[11px] leading-none text-gray-600 dark:text-gray-400"
          :style="{ gridColumn: 1, gridRow: di + 2 }"
        >
          {{ label }}
        </span>

        <template v-for="month in desktopMonths" :key="`cells-${month.month}`">
          <template v-for="(week, wi) in month.weeks" :key="wi">
            <div
              v-for="(day, di) in week"
              :key="di"
              class="calendar-cell aspect-square rounded-[3px]"
              :class="cellClass(day)"
              :style="{ gridColumn: month.startColumn + wi, gridRow: di + 2 }"
              @mouseenter="onCellEnter($event, day)"
              @mousemove="onCellMove"
              @mouseleave="onCellLeave"
              @click="selectDay(day)"
            />
          </template>
        </template>
      </div>
    </div>

    <div
      v-if="!isPending && !visibleError && !hasNoActivity"
      class="flex items-center justify-end gap-1.5"
    >
      <span class="text-[11px] text-gray-500 dark:text-gray-500">Less</span>
      <div
        v-for="intensity in [0, 1, 2, 3, 4] as const"
        :key="intensity"
        class="h-3.5 w-3.5 rounded-[3px]"
        :class="intensityColor(intensity)"
      />
      <span class="text-[11px] text-gray-500 dark:text-gray-500">More</span>
    </div>

    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition-all duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div
        v-if="selectedDay"
        class="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-gray-200/70 bg-gray-500/5 px-4 py-3 dark:border-gray-700/70 dark:bg-gray-400/5"
      >
        <div class="flex min-w-0 flex-1 items-center gap-2 sm:flex-none">
          <UIcon
            name="i-lucide-calendar-days"
            class="h-4 w-4 shrink-0 text-gray-600 dark:text-gray-400"
          />
          <span class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
            {{ dayTitle(selectedDay) }}
          </span>
          <UBadge color="neutral" variant="subtle" size="sm" class="shrink-0">
            {{ selectedDay.count }} total
          </UBadge>
        </div>

        <div class="order-last flex w-full flex-wrap items-center gap-2 sm:order-none sm:w-auto">
          <UBadge
            v-for="[op, count] in breakdownEntries(selectedDay.breakdown)"
            :key="op"
            :color="OP_BADGE_COLORS[op] ?? 'neutral'"
            variant="subtle"
            size="sm"
          >
            {{ OP_LABELS[op] ?? op }}: {{ count }}
          </UBadge>
        </div>

        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          size="xs"
          class="ml-auto shrink-0 justify-center"
          aria-label="Close day details"
          @click="selectedDay = null"
        />
      </div>
    </Transition>
  </section>

  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-100"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-75"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="tooltipVisible && tooltipDay"
        class="pointer-events-none fixed z-50 rounded-md bg-gray-900 px-2 py-1 text-xs font-medium whitespace-nowrap text-white shadow-md dark:bg-gray-100 dark:text-gray-900"
        :style="{ left: `${tooltipX + 12}px`, top: `${tooltipY - 32}px` }"
      >
        {{ tooltipText }}
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.calendar-cell {
  transition:
    transform 120ms ease,
    box-shadow 120ms ease,
    background-color 80ms ease;
}

.calendar-cell.interactive {
  cursor: pointer;
}

.calendar-cell.interactive:hover {
  transform: scale(1.2);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.25);
  position: relative;
  z-index: 1;
}

.calendar-cell.selected {
  box-shadow:
    0 0 0 2px var(--ui-bg),
    0 0 0 3.5px rgba(107, 114, 128, 0.9);
  position: relative;
  z-index: 2;
}

.calendar-cell-mobile {
  transition:
    background-color 80ms ease,
    box-shadow 120ms ease;
}

.calendar-cell-mobile.interactive:active {
  filter: brightness(0.85);
}

.calendar-cell-mobile.selected {
  box-shadow:
    0 0 0 2px var(--ui-bg),
    0 0 0 3.5px rgba(107, 114, 128, 0.9);
  position: relative;
  z-index: 2;
}

:is(.dark) .calendar-cell.selected,
:is(.dark) .calendar-cell-mobile.selected {
  box-shadow:
    0 0 0 2px var(--ui-bg),
    0 0 0 3.5px rgba(209, 213, 219, 0.8);
}
</style>
