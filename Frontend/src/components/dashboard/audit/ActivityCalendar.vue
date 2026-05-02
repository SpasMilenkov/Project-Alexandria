<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useMediaQuery } from "@vueuse/core";
import { useQuery } from "@pinia/colada";
import { OperationType } from "@/api/activity";
import { activitySummary } from "@/queries/activities";
import { type CalendarDay, useActivityCalendar } from "@/composables/useActivityCalendar";

// Responsive breakpoint
const isMobile = useMediaQuery("(max-width: 767px)");

// Year + month state
const currentYear = new Date().getFullYear();
const currentMonth = new Date().getMonth();
const MIN_YEAR = currentYear - 4;

const selectedYear = ref(currentYear);
const selectedMonth = ref(currentMonth);

// When the user changes year via desktop nav, snap month back to "now" if in
// the current year, otherwise snap to January so the view makes sense.
watch(selectedYear, (yr) => {
  selectedMonth.value = yr === currentYear ? currentMonth : 0;
  selectedDay.value = null;
});

// Desktop year nav
const canGoBack = computed(() => selectedYear.value > MIN_YEAR);
const canGoForward = computed(() => selectedYear.value < currentYear);

const prevYear = () => {
  if (canGoBack.value) {
    selectedYear.value--;
    selectedDay.value = null;
  }
};
const nextYear = () => {
  if (canGoForward.value) {
    selectedYear.value++;
    selectedDay.value = null;
  }
};

// Mobile month nav — wraps years automatically
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

// Operation filter
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

// Data fetching — always full year; mobile slicing is purely client-side
// oxlint-disable-next-line sort-keys
const { data, isLoading, error } = useQuery(activitySummary, () => ({
  startDate: new Date(Date.UTC(selectedYear.value, 0, 1)),
  endDate: new Date(Date.UTC(selectedYear.value + 1, 0, 1)),
}));

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const visibleError = computed(() =>
  error.value && !isAuthError(error.value) ? error.value : null,
);

// Calendar composable — full year data
const { weeks, monthLabelByWeek, totalFiltered } = useActivityCalendar(
  data,
  selectedYear,
  selectedOperation,
);

// Mobile: slice full-year weeks to just the selected month.
// Weeks straddling a boundary are kept — foreign-month cells are dimmed.
const mobileWeeks = computed(() =>
  weeks.value.filter((week) =>
    week.some((day) => !day.isPadding && day.date?.getMonth() === selectedMonth.value),
  ),
);

const isForeignMonthCell = (day: CalendarDay): boolean =>
  !day.isPadding && day.date?.getMonth() !== selectedMonth.value;

// Count for the currently visible month on mobile (replaces full-year total)
const mobileMonthTotal = computed(() =>
  mobileWeeks.value
    .flat()
    .filter((d) => !d.isPadding && d.date?.getMonth() === selectedMonth.value)
    .reduce((sum, d) => sum + d.count, 0),
);

const displayTotal = computed(() =>
  isMobile.value ? mobileMonthTotal.value : totalFiltered.value,
);

// Day selection
const selectedDay = ref<CalendarDay | null>(null);

const selectDay = (day: CalendarDay) => {
  if (day.isPadding || day.count === 0) return;
  selectedDay.value = selectedDay.value?.dayOfYear === day.dayOfYear ? null : day;
};

// Hover tooltip — desktop only; disabled on mobile to avoid phantom tooltips.
const tooltipVisible = ref(false);
const tooltipX = ref(0);
const tooltipY = ref(0);
const tooltipDay = ref<CalendarDay | null>(null);

const onCellEnter = (event: MouseEvent, day: CalendarDay) => {
  if (isMobile.value) return; // no hover tooltips on touch
  if (day.isPadding || day.count === 0) return;
  tooltipDay.value = day;
  tooltipVisible.value = true;
  positionTooltip(event);
};

const onCellMove = (event: MouseEvent) => {
  if (isMobile.value) return;
  if (tooltipVisible.value) positionTooltip(event);
};

const onCellLeave = () => {
  tooltipVisible.value = false;
  tooltipDay.value = null;
};

const positionTooltip = (event: MouseEvent) => {
  tooltipX.value = event.clientX;
  tooltipY.value = event.clientY;
};

const tooltipText = computed(() => {
  const day = tooltipDay.value;
  if (!day?.date) return "";
  const dateStr = day.date.toLocaleDateString(undefined, { day: "numeric", month: "short" });
  return day.count === 0
    ? `No activity · ${dateStr}`
    : `${day.count} action${day.count !== 1 ? "s" : ""} · ${dateStr}`;
});

// Cell appearance
const CELL_COLORS: Record<string, readonly string[]> = {
  all: [
    "bg-neutral-200/60 dark:bg-neutral-700/40",
    "bg-emerald-200 dark:bg-emerald-800",
    "bg-emerald-400 dark:bg-emerald-600",
    "bg-emerald-600 dark:bg-emerald-400",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Create]: [
    "bg-neutral-200/60 dark:bg-neutral-700/40",
    "bg-emerald-200 dark:bg-emerald-800",
    "bg-emerald-400 dark:bg-emerald-600",
    "bg-emerald-600 dark:bg-emerald-400",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Read]: [
    "bg-neutral-200/60 dark:bg-neutral-700/40",
    "bg-sky-200 dark:bg-sky-800",
    "bg-sky-400 dark:bg-sky-600",
    "bg-sky-600 dark:bg-sky-400",
    "bg-sky-800 dark:bg-sky-300",
  ],
  [OperationType.Update]: [
    "bg-neutral-200/60 dark:bg-neutral-700/40",
    "bg-amber-200 dark:bg-amber-800",
    "bg-amber-400 dark:bg-amber-600",
    "bg-amber-600 dark:bg-amber-400",
    "bg-amber-800 dark:bg-amber-300",
  ],
  [OperationType.Delete]: [
    "bg-neutral-200/60 dark:bg-neutral-700/40",
    "bg-rose-200 dark:bg-rose-800",
    "bg-rose-400 dark:bg-rose-600",
    "bg-rose-600 dark:bg-rose-400",
    "bg-rose-800 dark:bg-rose-300",
  ],
};

const cellColorKey = computed(() =>
  selectedOperation.value === null ? "all" : String(selectedOperation.value),
);

// Desktop heatmap: dim foreign-month boundary cells
const cellColorClass = (day: CalendarDay): string => {
  if (day.isPadding) return "opacity-0 pointer-events-none";
  const base = CELL_COLORS[cellColorKey.value]?.[day.intensity] ?? CELL_COLORS.all[0];
  if (isMobile.value && isForeignMonthCell(day)) return `${base} opacity-30`;
  return base;
};

// Mobile calendar grid: foreign-month cells always render as neutral neutral
// (they're visible for context, not for reading intensity)
const mobileCellColorClass = (day: CalendarDay): string => {
  if (day.isPadding) return "opacity-0 pointer-events-none";
  if (isForeignMonthCell(day)) return "bg-neutral-200/40 dark:bg-neutral-700/20";
  return CELL_COLORS[cellColorKey.value]?.[day.intensity] ?? CELL_COLORS.all[0];
};

// Desktop heatmap legend cell size
const cellSizeClass = "w-3 h-3";

// Mobile calendar day-of-week column headers (Mon–Sun)
const MOBILE_DAY_HEADERS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

// Returns the text color class for a day number in the mobile calendar grid.
// At intensity 3–4 in light mode the background becomes dark enough that
// white text is required; dark mode uses lighter backgrounds so dark text works.
const mobileDayNumberClass = (day: CalendarDay): string => {
  if (isForeignMonthCell(day)) return "text-neutral-400/60 dark:text-neutral-500/50";
  if (day.count === 0) return "text-neutral-400 dark:text-neutral-500";
  if (day.intensity >= 3) return "text-white dark:text-neutral-800";
  return "text-neutral-700 dark:text-neutral-200";
};

const mobileCellInteractiveClass = (day: CalendarDay): string =>
  !day.isPadding && day.count > 0 && !isForeignMonthCell(day) ? "interactive-mobile" : "";

// Detail panel helpers
const OP_LABELS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]: "Create",
  [OperationType.Read]: "Read",
  [OperationType.Update]: "Update",
  [OperationType.Delete]: "Delete",
};

type BadgeColor = "success" | "error" | "warning" | "info" | "neutral";

const OP_BADGE_COLORS: Partial<Record<OperationType, BadgeColor>> = {
  [OperationType.Create]: "success",
  [OperationType.Read]: "neutral",
  [OperationType.Update]: "warning",
  [OperationType.Delete]: "error",
};

const DAY_LABELS = ["Mon", "", "Wed", "", "Fri", "", ""];

const formatDate = (day: CalendarDay): string => {
  if (!day.date) return "";
  return day.date.toLocaleDateString(undefined, { month: "long", day: "numeric", year: "numeric" });
};

const breakdownEntries = (
  breakdown: Partial<Record<OperationType, number>>,
): [OperationType, number][] =>
  (Object.entries(breakdown) as [string, number][])
    .map(([k, v]) => [Number(k) as OperationType, v] as [OperationType, number])
    .filter(([, v]) => v > 0);
</script>

<template>
  <div class="border-b border-neutral-200/70 dark:border-neutral-700/70 px-6 py-5 space-y-4">
    <!-- Header row -->
    <div class="flex items-center justify-between gap-3 flex-wrap">
      <!-- Mobile: month navigator -->
      <div v-if="isMobile" class="flex items-center gap-2">
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          size="xs"
          :disabled="!canGoBackMonth"
          aria-label="Previous month"
          @click="prevMonth"
        />
        <span class="text-sm font-semibold tabular-nums w-36 text-center select-none">
          {{ mobileMonthLabel }}
        </span>
        <UButton
          icon="i-lucide-chevron-right"
          color="neutral"
          variant="ghost"
          size="xs"
          :disabled="!canGoForwardMonth"
          aria-label="Next month"
          @click="nextMonth"
        />
        <UBadge color="neutral" variant="subtle" size="sm">
          {{ displayTotal.toLocaleString() }} actions
        </UBadge>
      </div>

      <!-- Desktop: year navigator (unchanged) -->
      <div v-else class="flex items-center gap-2">
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          size="xs"
          :disabled="!canGoBack"
          aria-label="Previous year"
          @click="prevYear"
        />
        <span class="text-sm font-semibold tabular-nums w-10 text-center select-none">
          {{ selectedYear }}
        </span>
        <UButton
          icon="i-lucide-chevron-right"
          color="neutral"
          variant="ghost"
          size="xs"
          :disabled="!canGoForward"
          aria-label="Next year"
          @click="nextYear"
        />
        <UBadge color="neutral" variant="subtle" size="sm">
          {{ displayTotal.toLocaleString() }} actions
        </UBadge>
      </div>

      <!-- Operation filter buttons (shared) -->
      <div class="flex items-center gap-1 flex-wrap">
        <UButton
          v-for="filter in FILTERS"
          :key="String(filter.value)"
          :variant="selectedOperation === filter.value ? 'solid' : 'outline'"
          :color="selectedOperation === filter.value ? 'primary' : 'neutral'"
          size="xs"
          @click="setFilter(filter.value)"
        >
          {{ filter.label }}
        </UButton>
      </div>
    </div>

    <!-- Mobile: traditional calendar grid -->
    <div v-if="isMobile" class="w-full space-y-2">
      <!-- Loading skeleton -->
      <div v-if="isLoading" class="space-y-1">
        <div class="grid grid-cols-7 gap-1">
          <USkeleton v-for="i in 7" :key="i" class="h-4 rounded-sm opacity-30" />
        </div>
        <div class="grid grid-cols-7 gap-1">
          <USkeleton
            v-for="i in 35"
            :key="i"
            class="aspect-square rounded-lg"
            :class="i % 5 === 0 ? 'opacity-10' : i % 3 === 0 ? 'opacity-30' : 'opacity-50'"
          />
        </div>
      </div>

      <!-- Error -->
      <UAlert
        v-else-if="visibleError"
        color="error"
        variant="subtle"
        icon="i-lucide-alert-circle"
        title="Failed to load activity"
        :description="visibleError.message"
      />

      <!-- Empty state -->
      <div
        v-else-if="!isLoading && displayTotal === 0 && data"
        class="flex flex-col items-center justify-center py-10 gap-3 text-center"
      >
        <UIcon name="i-lucide-calendar-x" class="w-10 h-10 text-muted" />
        <p class="text-sm text-muted">No activity recorded for {{ mobileMonthLabel }}</p>
      </div>

      <template v-else>
        <!-- Day-of-week header row -->
        <div class="grid grid-cols-7 gap-1">
          <div
            v-for="label in MOBILE_DAY_HEADERS"
            :key="label"
            class="text-center text-[11px] font-semibold text-muted select-none py-0.5 tracking-wide"
          >
            {{ label }}
          </div>
        </div>

        <!-- Calendar cells — 7 per row, one row per week -->
        <div class="grid grid-cols-7 gap-1">
          <div
            v-for="(day, idx) in mobileWeeks.flat()"
            :key="idx"
            class="calendar-cell-mobile relative flex items-start justify-start p-1.5 rounded-lg aspect-square"
            :class="[
              mobileCellColorClass(day),
              mobileCellInteractiveClass(day),
              selectedDay?.dayOfYear === day.dayOfYear ? 'selected-mobile' : '',
            ]"
            @click="selectDay(day)"
          >
            <!-- Day number shown top-left inside the cell -->
            <span
              v-if="!day.isPadding"
              class="text-[11px] leading-none font-medium select-none"
              :class="mobileDayNumberClass(day)"
            >
              {{ day.date?.getDate() }}
            </span>
          </div>
        </div>

        <!-- Legend -->
        <div class="flex items-center gap-2 justify-end pt-1">
          <span class="text-[10px] text-muted">Less</span>
          <div
            v-for="intensity in [0, 1, 2, 3, 4] as const"
            :key="intensity"
            class="w-4 h-4 rounded-md"
            :class="CELL_COLORS[cellColorKey]?.[intensity]"
          />
          <span class="text-[10px] text-muted">More</span>
        </div>
      </template>
    </div>

    <!-- Desktop: GitHub-style heatmap -->
    <div v-else class="flex flex-col gap-3">
      <div class="overflow-x-auto -mx-1 px-1 flex">
        <div class="w-fit mx-auto">
          <!-- Loading skeleton -->
          <div v-if="isLoading" class="inline-flex gap-[3px]">
            <div class="w-7 shrink-0" />
            <div v-for="wi in 53" :key="wi" class="flex flex-col gap-[3px]">
              <USkeleton class="h-4 w-3 rounded-none opacity-0" />
              <USkeleton
                v-for="di in 7"
                :key="di"
                :class="['w-3 h-3 rounded-sm', (wi + di) % 3 === 0 ? 'opacity-20' : '']"
              />
            </div>
          </div>

          <!-- Error -->
          <UAlert
            v-else-if="visibleError"
            color="error"
            variant="subtle"
            icon="i-lucide-alert-circle"
            title="Failed to load activity"
            :description="visibleError.message"
          />

          <!-- Empty state -->
          <div
            v-else-if="!isLoading && displayTotal === 0 && data"
            class="flex flex-col items-center justify-center py-10 gap-3 text-center"
          >
            <UIcon name="i-lucide-calendar-x" class="w-10 h-10 text-muted" />
            <p class="text-sm text-muted">No activity recorded for {{ selectedYear }}</p>
          </div>

          <!-- Grid -->
          <div v-else class="inline-flex gap-1 min-w-max">
            <!-- Day-of-week labels -->
            <div class="flex flex-col shrink-0 w-7">
              <div class="h-5" />
              <div
                v-for="(label, i) in DAY_LABELS"
                :key="i"
                class="h-3 mb-[3px] text-[10px] text-muted flex items-center justify-end leading-none"
              >
                {{ label }}
              </div>
            </div>

            <!-- Week columns -->
            <div class="flex gap-[3px]">
              <div v-for="(week, wi) in weeks" :key="wi" class="flex flex-col gap-[3px]">
                <!-- Month label -->
                <div class="h-5 flex items-end pb-1">
                  <span
                    v-if="monthLabelByWeek[wi]"
                    class="text-[10px] text-muted leading-none whitespace-nowrap"
                  >
                    {{ monthLabelByWeek[wi] }}
                  </span>
                </div>

                <!-- Cells -->
                <div
                  v-for="(day, di) in week"
                  :key="di"
                  class="calendar-cell w-3 h-3 rounded-sm"
                  :class="[
                    cellColorClass(day),
                    !day.isPadding && day.count > 0 ? 'interactive' : '',
                    selectedDay?.dayOfYear === day.dayOfYear ? 'selected' : '',
                  ]"
                  @mouseenter="onCellEnter($event, day)"
                  @mousemove="onCellMove"
                  @mouseleave="onCellLeave"
                  @click="selectDay(day)"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Legend -->
      <div class="flex items-center gap-2 justify-end">
        <span class="text-[10px] text-muted">Less</span>
        <div
          v-for="intensity in [0, 1, 2, 3, 4] as const"
          :key="intensity"
          :class="[cellSizeClass, 'rounded-sm', CELL_COLORS[cellColorKey]?.[intensity]]"
        />
        <span class="text-[10px] text-muted">More</span>
      </div>
    </div>

    <!-- Desktop: inline detail panel -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out overflow-hidden"
      enter-from-class="opacity-0 max-h-0"
      enter-to-class="opacity-100 max-h-32"
      leave-active-class="transition-all duration-150 ease-in overflow-hidden"
      leave-from-class="opacity-100 max-h-32"
      leave-to-class="opacity-0 max-h-0"
    >
      <div
        v-if="!isMobile && selectedDay"
        class="flex items-center gap-3 flex-wrap rounded-lg bg-white/40 dark:bg-white/5 border border-neutral-200/70 dark:border-neutral-700/70 px-4 py-3"
      >
        <div class="flex items-center gap-2 shrink-0">
          <UIcon name="i-lucide-calendar-days" class="w-4 h-4 text-muted" />
          <span class="text-sm font-medium">{{ formatDate(selectedDay) }}</span>
          <UBadge color="neutral" variant="subtle" size="sm">
            {{ selectedDay.count }} total
          </UBadge>
        </div>
        <div class="w-px h-4 bg-neutral-300/60 dark:bg-neutral-600/60 shrink-0" />
        <div class="flex items-center gap-2 flex-wrap">
          <UBadge
            v-for="[op, count] in breakdownEntries(selectedDay.breakdown)"
            :key="op"
            :color="OP_BADGE_COLORS[op] ?? 'neutral'"
            variant="soft"
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
          class="ml-auto shrink-0"
          aria-label="Close"
          @click="selectedDay = null"
        />
      </div>
    </Transition>
  </div>

  <!-- Mobile: bottom sheet + desktop tooltip -->
  <Teleport to="body">
    <!-- Hover tooltip — desktop only, single shared instance -->
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
        class="fixed z-50 pointer-events-none px-2 py-1 rounded-md text-xs font-medium bg-neutral-900/90 dark:bg-neutral-100/90 text-white dark:text-neutral-900 shadow-md whitespace-nowrap"
        :style="{ left: `${tooltipX + 12}px`, top: `${tooltipY - 28}px` }"
      >
        {{ tooltipText }}
      </div>
    </Transition>

    <!-- Mobile bottom sheet -->
    <Transition
      enter-active-class="transition-all duration-300 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-all duration-200 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div v-if="isMobile && selectedDay" class="fixed inset-0 z-50 flex items-end">
        <!-- Scrim -->
        <div class="absolute inset-0 bg-black/40" @click="selectedDay = null" />

        <!-- Sheet -->
        <Transition
          enter-active-class="transition-transform duration-300 ease-out"
          enter-from-class="translate-y-full"
          enter-to-class="translate-y-0"
          leave-active-class="transition-transform duration-200 ease-in"
          leave-from-class="translate-y-0"
          leave-to-class="translate-y-full"
        >
          <div
            v-if="selectedDay"
            class="relative w-full rounded-t-2xl bg-white/95 dark:bg-neutral-900/95 shadow-2xl border-t border-neutral-200/70 dark:border-neutral-700/70 px-5 pt-3 pb-8 space-y-4"
          >
            <!-- Drag handle -->
            <div class="w-10 h-1 rounded-full bg-neutral-300 dark:bg-neutral-600 mx-auto" />

            <!-- Date + total -->
            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-2 min-w-0">
                <UIcon name="i-lucide-calendar-days" class="w-5 h-5 text-muted shrink-0" />
                <span class="text-base font-semibold truncate">{{ formatDate(selectedDay) }}</span>
              </div>
              <UBadge color="neutral" variant="subtle" size="sm" class="shrink-0">
                {{ selectedDay.count }} total
              </UBadge>
            </div>

            <!-- Divider -->
            <div class="h-px bg-neutral-200/70 dark:bg-neutral-700/70" />

            <!-- Breakdown badges -->
            <div class="flex flex-wrap gap-2">
              <UBadge
                v-for="[op, count] in breakdownEntries(selectedDay.breakdown)"
                :key="op"
                :color="OP_BADGE_COLORS[op] ?? 'neutral'"
                variant="soft"
              >
                {{ OP_LABELS[op] ?? op }}: {{ count }}
              </UBadge>
            </div>

            <!-- Dismiss -->
            <UButton block variant="outline" color="neutral" @click="selectedDay = null">
              Dismiss
            </UButton>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
/* ── Desktop heatmap cells */
.calendar-cell {
  transition:
    transform 120ms ease,
    box-shadow 120ms ease,
    background-color 80ms ease;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.08);
}

.calendar-cell.interactive {
  cursor: pointer;
}

.calendar-cell.interactive:hover {
  transform: scale(1.35);
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.15),
    0 2px 6px rgba(0, 0, 0, 0.25);
  z-index: 1;
  position: relative;
}

.calendar-cell.selected {
  transform: scale(1.35);
  box-shadow:
    0 0 0 1.5px rgba(156, 163, 175, 0.8),
    0 2px 8px rgba(0, 0, 0, 0.3);
  position: relative;
  z-index: 2;
}

:is(.dark) .calendar-cell.interactive:hover {
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.12),
    0 2px 6px rgba(0, 0, 0, 0.5);
}

:is(.dark) .calendar-cell.selected {
  box-shadow:
    0 0 0 1.5px rgba(209, 213, 219, 0.6),
    0 2px 8px rgba(0, 0, 0, 0.5);
}

/* Mobile calendar grid cells */
.calendar-cell-mobile {
  transition:
    background-color 80ms ease,
    box-shadow 120ms ease;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06);
}

.calendar-cell-mobile.interactive-mobile {
  cursor: pointer;
}

/* Active press feedback on touch */
.calendar-cell-mobile.interactive-mobile:active {
  filter: brightness(0.85);
}

/* Selected state: ring instead of scale (cells are too large to scale) */
.calendar-cell-mobile.selected-mobile {
  box-shadow:
    0 0 0 2px rgba(107, 114, 128, 0.9),
    0 2px 8px rgba(0, 0, 0, 0.25);
  position: relative;
  z-index: 2;
}

:is(.dark) .calendar-cell-mobile.selected-mobile {
  box-shadow:
    0 0 0 2px rgba(209, 213, 219, 0.7),
    0 2px 8px rgba(0, 0, 0, 0.5);
}
</style>
