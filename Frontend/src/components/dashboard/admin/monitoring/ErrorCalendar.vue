<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import {
  type CalendarBreakdownEntry,
  type ErrorCalendarDay,
  useErrorCalendar,
} from "@/composables/useErrorCalendar";
import { ServiceType } from "@/enums";
import { errorCalendar } from "@/queries/monitoring";
import {
  SEVERITY_BADGE_COLORS,
  SEVERITY_LABELS,
  SERVICE_LABELS,
} from "@/utils/monitoring-display.utils";

const emit = defineEmits<{ viewDay: [date: Date] }>();

const isMobile = useMediaQuery("(max-width: 767px)");

const currentYear = new Date().getFullYear();
const currentMonth = new Date().getMonth();
const MIN_YEAR = currentYear - 4;

const selectedYear = ref(currentYear);
const selectedMonth = ref(currentMonth);

const HEATMAP_CELL = "w-3.5 h-3.5 xl:w-4 xl:h-4";
const HEATMAP_GAP = "gap-[4px] xl:gap-[5px]";
const HEATMAP_ROW = "h-3.5 xl:h-4";

watch(selectedYear, (yr) => {
  selectedMonth.value = yr === currentYear ? currentMonth : 0;
  selectedDay.value = null;
});

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

// Service filter
const SERVICES: ServiceType[] = [
  ServiceType.Api,
  ServiceType.MediaPreviews,
  ServiceType.DocumentPreviews,
  ServiceType.Transpilation,
  ServiceType.Lyrics,
  ServiceType.MediaMetadata,
];

const FILTERS: { label: string; value: ServiceType | null }[] = [
  { label: "All", value: null },
  ...SERVICES.map((service) => ({ label: SERVICE_LABELS[service], value: service })),
];

// Compact options for the mobile <USelect> — same list, undefined instead of
// null so it matches the sentinel convention used across the app's filters.
const MOBILE_SERVICE_OPTIONS = FILTERS.map((filter) => ({
  label: filter.value === null ? "All services" : filter.label,
  value: filter.value ?? undefined,
}));

const selectedService = ref<ServiceType | null>(null);

const setFilter = (service: ServiceType | null) => {
  selectedService.value = service;
  selectedDay.value = null;
};

const mobileServiceFilter = computed<ServiceType | undefined>({
  get: () => selectedService.value ?? undefined,
  set: (value) => setFilter(value ?? null),
});

// Data fetching — full year; mobile month view slices client-side
const range = computed(() => ({
  from: new Date(Date.UTC(selectedYear.value, 0, 1)),
  to: new Date(Date.UTC(selectedYear.value + 1, 0, 1)),
}));

const { data, isLoading, error } = useQuery(errorCalendar, () => range.value);

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

const { weeks, monthLabelByWeek, totalFiltered } = useErrorCalendar(
  data,
  selectedYear,
  selectedService,
);

const mobileWeeks = computed(() =>
  weeks.value.filter((week) =>
    week.some((day) => !day.isPadding && day.date?.getMonth() === selectedMonth.value),
  ),
);

const isForeignMonthCell = (day: ErrorCalendarDay): boolean =>
  !day.isPadding && day.date?.getMonth() !== selectedMonth.value;

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
const selectedDay = ref<ErrorCalendarDay | null>(null);

const selectDay = (day: ErrorCalendarDay) => {
  if (day.isPadding || day.count === 0) return;
  selectedDay.value = selectedDay.value?.dayOfYear === day.dayOfYear ? null : day;
};

const viewSelectedDay = () => {
  const date = selectedDay.value?.date;
  if (!date) return;
  emit("viewDay", date);
  selectedDay.value = null;
};

// Hover tooltip — desktop only
const tooltipVisible = ref(false);
const tooltipX = ref(0);
const tooltipY = ref(0);
const tooltipDay = ref<ErrorCalendarDay | null>(null);

const onCellEnter = (event: MouseEvent, day: ErrorCalendarDay) => {
  if (isMobile.value) return;
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
    ? `No incidents · ${dateStr}`
    : `${day.count} incident${day.count === 1 ? "" : "s"} · ${dateStr}`;
});

// Hue family reads the worst severity present that day (any Failure → red,
// only Partial/Degraded → amber); intensity stays count-scaled within family.
const EMPTY_CELL = "bg-neutral-200/60 dark:bg-neutral-700/40";

const RED_RAMP: readonly string[] = [
  EMPTY_CELL,
  "bg-red-200 dark:bg-red-900",
  "bg-red-400 dark:bg-red-700",
  "bg-red-600 dark:bg-red-400",
  "bg-red-800 dark:bg-red-300",
];

const AMBER_RAMP: readonly string[] = [
  EMPTY_CELL,
  "bg-amber-200 dark:bg-amber-900",
  "bg-amber-400 dark:bg-amber-700",
  "bg-amber-600 dark:bg-amber-400",
  "bg-amber-800 dark:bg-amber-300",
];

const rampFor = (day: ErrorCalendarDay): readonly string[] =>
  day.hasFailure ? RED_RAMP : AMBER_RAMP;

const cellColorClass = (day: ErrorCalendarDay): string => {
  if (day.isPadding) return "opacity-0 pointer-events-none";
  const base = day.count === 0 ? EMPTY_CELL : rampFor(day)[day.intensity];
  if (isMobile.value && isForeignMonthCell(day)) return `${base} opacity-30`;
  return base;
};

const mobileCellColorClass = (day: ErrorCalendarDay): string => {
  if (day.isPadding) return "opacity-0 pointer-events-none";
  if (isForeignMonthCell(day)) return "bg-neutral-200/40 dark:bg-neutral-700/20";
  return day.count === 0 ? EMPTY_CELL : rampFor(day)[day.intensity];
};

const cellSizeClass = "w-3 h-3";

const MOBILE_DAY_HEADERS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

const mobileDayNumberClass = (day: ErrorCalendarDay): string => {
  if (isForeignMonthCell(day)) return "text-neutral-400 dark:text-neutral-500";
  if (day.count === 0) return "text-neutral-400 dark:text-neutral-500";
  if (day.intensity >= 3) return "text-white dark:text-neutral-900";
  return "text-neutral-700 dark:text-neutral-200";
};

const mobileCellInteractiveClass = (day: ErrorCalendarDay): string =>
  !day.isPadding && day.count > 0 && !isForeignMonthCell(day) ? "interactive-mobile" : "";

const DAY_LABELS = ["Mon", "", "Wed", "", "Fri", "", ""];

const formatDate = (day: ErrorCalendarDay): string => {
  if (!day.date) return "";
  return day.date.toLocaleDateString(undefined, { month: "long", day: "numeric", year: "numeric" });
};

const breakdownEntries = (day: ErrorCalendarDay): CalendarBreakdownEntry[] =>
  day.entries.filter((entry) => entry.count > 0);
</script>

<template>
  <div class="space-y-4">
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
          {{ displayTotal.toLocaleString() }} incidents
        </UBadge>
      </div>

      <!-- Desktop: year navigator -->
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
          {{ displayTotal.toLocaleString() }} incidents
        </UBadge>
      </div>

      <!-- Desktop: filter chips -->
      <div class="hidden sm:flex items-center gap-1 flex-wrap">
        <UButton
          v-for="filter in FILTERS"
          :key="String(filter.value)"
          :variant="selectedService === filter.value ? 'solid' : 'outline'"
          :color="selectedService === filter.value ? 'primary' : 'neutral'"
          size="xs"
          @click="setFilter(filter.value)"
        >
          {{ filter.label }}
        </UButton>
      </div>

      <!-- Mobile: compact select, same control style as other filter menus -->
      <USelect
        v-model="mobileServiceFilter"
        :items="MOBILE_SERVICE_OPTIONS"
        placeholder="Service"
        size="xs"
        class="sm:hidden w-40"
      />
    </div>

    <!-- Mobile: traditional calendar grid -->
    <div v-if="isMobile" class="w-full space-y-2">
      <div v-if="isLoading" class="flex items-center justify-center py-16">
        <UIcon
          name="i-mdi-loading"
          class="w-6 h-6 text-neutral-400 dark:text-neutral-500 animate-spin"
        />
      </div>

      <UAlert
        v-else-if="visibleError"
        color="error"
        variant="subtle"
        icon="i-lucide-alert-circle"
        title="Failed to load incident calendar"
        :description="visibleError.message"
      />

      <div
        v-else-if="!isLoading && displayTotal === 0 && data"
        class="flex flex-col items-center justify-center py-10 gap-3 text-center"
      >
        <UIcon
          name="i-lucide-calendar-x"
          class="w-10 h-10 text-neutral-400 dark:text-neutral-600"
        />
        <p class="text-sm text-neutral-500 dark:text-neutral-400">
          No incidents recorded for {{ mobileMonthLabel }}
        </p>
      </div>

      <template v-else>
        <div class="grid grid-cols-7 gap-1">
          <div
            v-for="label in MOBILE_DAY_HEADERS"
            :key="label"
            class="text-center text-[11px] font-semibold text-neutral-500 dark:text-neutral-400 select-none py-0.5 tracking-wide"
          >
            {{ label }}
          </div>
        </div>

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
            <span
              v-if="!day.isPadding"
              class="text-[11px] leading-none font-medium select-none"
              :class="mobileDayNumberClass(day)"
            >
              {{ day.date?.getDate() }}
            </span>
          </div>
        </div>

        <div class="flex flex-col items-end gap-1 pt-1">
          <div class="flex items-center gap-1.5">
            <span class="text-[10px] text-neutral-500 dark:text-neutral-400 mr-1">Failure</span>
            <div
              v-for="intensity in [0, 1, 2, 3, 4] as const"
              :key="`f${intensity}`"
              class="w-4 h-4 rounded-md"
              :class="RED_RAMP[intensity]"
            />
          </div>
          <div class="flex items-center gap-1.5">
            <span class="text-[10px] text-neutral-500 dark:text-neutral-400 mr-1">Degraded</span>
            <div
              v-for="intensity in [0, 1, 2, 3, 4] as const"
              :key="`a${intensity}`"
              class="w-4 h-4 rounded-md"
              :class="AMBER_RAMP[intensity]"
            />
          </div>
        </div>
      </template>
    </div>

    <!-- Desktop: GitHub-style heatmap -->
    <!-- Desktop: GitHub-style heatmap -->
    <div v-else class="flex flex-col gap-3">
      <div class="overflow-x-auto -mx-1 px-1 flex">
        <div class="w-fit mx-auto">
          <div
            v-if="isLoading"
            class="flex items-center justify-center py-16 w-[970px] xl:w-[1125px]"
          >
            <UIcon
              name="i-mdi-loading"
              class="w-6 h-6 text-neutral-400 dark:text-neutral-500 animate-spin"
            />
          </div>

          <UAlert
            v-else-if="visibleError"
            color="error"
            variant="subtle"
            icon="i-lucide-alert-circle"
            title="Failed to load incident calendar"
            :description="visibleError.message"
          />

          <div
            v-else-if="!isLoading && displayTotal === 0 && data"
            class="flex flex-col items-center justify-center py-10 gap-3 text-center"
          >
            <UIcon
              name="i-lucide-calendar-x"
              class="w-10 h-10 text-neutral-400 dark:text-neutral-600"
            />
            <p class="text-sm text-neutral-500 dark:text-neutral-400">
              No incidents recorded for {{ selectedYear }}
            </p>
          </div>

          <div v-else class="inline-flex gap-1.5 min-w-max">
            <!-- Weekday labels — spacer height, gap and row height mirror the week
                 columns exactly; that's what keeps Mon/Wed/Fri on their rows. -->
            <div class="flex flex-col shrink-0 w-8" :class="HEATMAP_GAP">
              <div class="h-6" />
              <div
                v-for="(label, i) in DAY_LABELS"
                :key="i"
                class="text-[11px] text-neutral-500 dark:text-neutral-400 flex items-center justify-end leading-none"
                :class="HEATMAP_ROW"
              >
                {{ label }}
              </div>
            </div>

            <div class="flex" :class="HEATMAP_GAP">
              <div v-for="(week, wi) in weeks" :key="wi" class="flex flex-col" :class="HEATMAP_GAP">
                <div class="h-6 flex items-end pb-1">
                  <span
                    v-if="monthLabelByWeek[wi]"
                    class="text-[11px] text-neutral-500 dark:text-neutral-400 leading-none whitespace-nowrap"
                  >
                    {{ monthLabelByWeek[wi] }}
                  </span>
                </div>

                <div
                  v-for="(day, di) in week"
                  :key="di"
                  class="calendar-cell rounded-sm"
                  :class="[
                    HEATMAP_CELL,
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

      <div class="flex flex-col items-end gap-1">
        <div class="flex items-center gap-1.5">
          <span class="text-[11px] text-neutral-500 dark:text-neutral-400 mr-1">Failure</span>
          <div
            v-for="intensity in [0, 1, 2, 3, 4] as const"
            :key="`f${intensity}`"
            :class="[HEATMAP_CELL, 'rounded-sm', RED_RAMP[intensity]]"
          />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[11px] text-neutral-500 dark:text-neutral-400 mr-1">Degraded</span>
          <div
            v-for="intensity in [0, 1, 2, 3, 4] as const"
            :key="`a${intensity}`"
            :class="[HEATMAP_CELL, 'rounded-sm', AMBER_RAMP[intensity]]"
          />
        </div>
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
        class="flex items-center gap-3 flex-wrap rounded-lg frosted-glass glass-surface border border-neutral-200/70 dark:border-neutral-700/70 px-4 py-3"
      >
        <div class="flex items-center gap-2 shrink-0">
          <UIcon
            name="i-lucide-calendar-days"
            class="w-4 h-4 text-neutral-400 dark:text-neutral-500"
          />
          <span class="text-sm font-medium">{{ formatDate(selectedDay) }}</span>
          <UBadge color="neutral" variant="subtle" size="sm">
            {{ selectedDay.count }} total
          </UBadge>
        </div>
        <div class="w-px h-4 bg-neutral-300/60 dark:bg-neutral-600/60 shrink-0" />
        <div class="flex items-center gap-2 flex-wrap">
          <UBadge
            v-for="entry in breakdownEntries(selectedDay)"
            :key="`${entry.service}-${entry.severity}`"
            :color="SEVERITY_BADGE_COLORS[entry.severity]"
            variant="subtle"
            size="sm"
          >
            {{ SERVICE_LABELS[entry.service] ?? entry.service }} ·
            {{ SEVERITY_LABELS[entry.severity] }}: {{ entry.count }}
          </UBadge>
        </div>
        <UButton
          size="xs"
          color="primary"
          variant="outline"
          icon="i-lucide-list"
          class="ml-auto shrink-0"
          loading-auto
          @click="viewSelectedDay"
        >
          View events
        </UButton>
        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          size="xs"
          class="shrink-0"
          aria-label="Close"
          @click="selectedDay = null"
        />
      </div>
    </Transition>
  </div>

  <!-- Mobile bottom sheet + desktop tooltip -->
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
        class="fixed z-50 pointer-events-none px-2 py-1 rounded-md text-xs font-medium bg-neutral-900/90 dark:bg-neutral-100/90 text-white dark:text-neutral-900 shadow-md whitespace-nowrap"
        :style="{ left: `${tooltipX + 12}px`, top: `${tooltipY - 28}px` }"
      >
        {{ tooltipText }}
      </div>
    </Transition>

    <Transition
      enter-active-class="transition-all duration-300 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-all duration-200 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div v-if="isMobile && selectedDay" class="fixed inset-0 z-50 flex items-end">
        <div class="absolute inset-0 bg-black/40 frosted-glass" @click="selectedDay = null" />

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
            class="relative w-full rounded-t-2xl frosted-glass glass-surface-strong shadow-2xl border-t border-neutral-200/70 dark:border-neutral-700/70 px-5 pt-3 pb-8 space-y-4"
          >
            <div class="w-10 h-1 rounded-full bg-neutral-300 dark:bg-neutral-600 mx-auto" />

            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-2 min-w-0">
                <UIcon
                  name="i-lucide-calendar-days"
                  class="w-5 h-5 text-neutral-400 dark:text-neutral-500 shrink-0"
                />
                <span class="text-base font-semibold truncate">{{ formatDate(selectedDay) }}</span>
              </div>
              <UBadge color="neutral" variant="subtle" size="sm" class="shrink-0">
                {{ selectedDay.count }} total
              </UBadge>
            </div>

            <div class="h-px bg-neutral-200/70 dark:bg-neutral-700/70" />

            <div class="flex flex-wrap gap-2">
              <UBadge
                v-for="entry in breakdownEntries(selectedDay)"
                :key="`${entry.service}-${entry.severity}`"
                :color="SEVERITY_BADGE_COLORS[entry.severity]"
                variant="subtle"
              >
                {{ SERVICE_LABELS[entry.service] ?? entry.service }} ·
                {{ SEVERITY_LABELS[entry.severity] }}: {{ entry.count }}
              </UBadge>
            </div>

            <div class="flex gap-2">
              <UButton block color="primary" @click="viewSelectedDay"> View events </UButton>
              <UButton block variant="outline" color="neutral" @click="selectedDay = null">
                Dismiss
              </UButton>
            </div>
          </div>
        </Transition>
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

.calendar-cell-mobile {
  transition:
    background-color 80ms ease,
    box-shadow 120ms ease;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06);
}

.calendar-cell-mobile.interactive-mobile {
  cursor: pointer;
}

.calendar-cell-mobile.interactive-mobile:active {
  filter: brightness(0.85);
}

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
