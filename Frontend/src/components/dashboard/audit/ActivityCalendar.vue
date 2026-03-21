<script setup lang="ts">
import { computed, ref } from "vue";
import { useQuery } from "@pinia/colada";
import { OperationType } from "@/api/activity";
import { activitySummary } from "@/queries/activities";
import { type CalendarDay, useActivityCalendar } from "@/composables/useActivityCalendar";

const currentYear = new Date().getFullYear();
const MIN_YEAR = currentYear - 4;
const selectedYear = ref(currentYear);

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

const { data, isLoading, error } = useQuery(activitySummary, () => ({
  startDate: new Date(Date.UTC(selectedYear.value, 0, 1)),
  endDate: new Date(Date.UTC(selectedYear.value + 1, 0, 1)),
}));

// Suppress transient auth errors (401/403) — these are caused by token refresh
// cycles and resolve automatically; showing a banner would be misleading.
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

const { weeks, monthLabelByWeek, totalFiltered } = useActivityCalendar(
  data,
  selectedYear,
  selectedOperation,
);

const selectedDay = ref<CalendarDay | null>(null);

const selectDay = (day: CalendarDay) => {
  if (day.isPadding || day.count === 0) return;
  selectedDay.value = selectedDay.value?.dayOfYear === day.dayOfYear ? null : day;
};

const tooltipVisible = ref(false);
const tooltipX = ref(0);
const tooltipY = ref(0);
const tooltipDay = ref<CalendarDay | null>(null);

const onCellEnter = (event: MouseEvent, day: CalendarDay) => {
  if (day.isPadding || day.count === 0) return;
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

const positionTooltip = (event: MouseEvent) => {
  tooltipX.value = event.clientX;
  tooltipY.value = event.clientY;
};

const tooltipText = computed(() => {
  const day = tooltipDay.value;
  if (!day?.date) return "";
  const dateStr = day.date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
  return day.count === 0
    ? `No activity · ${dateStr}`
    : `${day.count} action${day.count !== 1 ? "s" : ""} · ${dateStr}`;
});

const CELL_COLORS: Record<string, readonly string[]> = {
  all: [
    "bg-gray-200/60 dark:bg-gray-700/40",
    "bg-emerald-200 dark:bg-emerald-800",
    "bg-emerald-400 dark:bg-emerald-600",
    "bg-emerald-600 dark:bg-emerald-400",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Create]: [
    "bg-gray-200/60 dark:bg-gray-700/40",
    "bg-emerald-200 dark:bg-emerald-800",
    "bg-emerald-400 dark:bg-emerald-600",
    "bg-emerald-600 dark:bg-emerald-400",
    "bg-emerald-800 dark:bg-emerald-300",
  ],
  [OperationType.Read]: [
    "bg-gray-200/60 dark:bg-gray-700/40",
    "bg-sky-200 dark:bg-sky-800",
    "bg-sky-400 dark:bg-sky-600",
    "bg-sky-600 dark:bg-sky-400",
    "bg-sky-800 dark:bg-sky-300",
  ],
  [OperationType.Update]: [
    "bg-gray-200/60 dark:bg-gray-700/40",
    "bg-amber-200 dark:bg-amber-800",
    "bg-amber-400 dark:bg-amber-600",
    "bg-amber-600 dark:bg-amber-400",
    "bg-amber-800 dark:bg-amber-300",
  ],
  [OperationType.Delete]: [
    "bg-gray-200/60 dark:bg-gray-700/40",
    "bg-rose-200 dark:bg-rose-800",
    "bg-rose-400 dark:bg-rose-600",
    "bg-rose-600 dark:bg-rose-400",
    "bg-rose-800 dark:bg-rose-300",
  ],
};

const cellColorClass = (day: CalendarDay): string => {
  if (day.isPadding) return "opacity-0 pointer-events-none";
  const key = selectedOperation.value === null ? "all" : String(selectedOperation.value);
  return CELL_COLORS[key]?.[day.intensity] ?? CELL_COLORS.all[0];
};

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
  return day.date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
};

const breakdownEntries = (
  breakdown: Partial<Record<OperationType, number>>,
): [OperationType, number][] =>
  (Object.entries(breakdown) as [string, number][])
    .map(([k, v]) => [Number(k) as OperationType, v] as [OperationType, number])
    .filter(([, v]) => v > 0);
</script>

<template>
  <div class="border-b border-gray-200/70 dark:border-gray-700/70 px-6 py-5 space-y-4">
    <div class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-2">
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
          {{ totalFiltered.toLocaleString() }} actions
        </UBadge>
      </div>

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
              class="w-3 h-3 rounded-sm"
              :class="{ 'opacity-20': (wi + di) % 3 === 0 }"
            />
          </div>
        </div>

        <!-- Error — auth/token errors are intentionally suppressed -->
        <UAlert
          v-else-if="visibleError"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Failed to load activity"
          :description="visibleError.message"
        />

        <!-- Empty year -->
        <div
          v-else-if="!isLoading && totalFiltered === 0 && data"
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

      <!-- Detail panel -->
      <Transition
        enter-active-class="transition-all duration-200 ease-out overflow-hidden"
        enter-from-class="opacity-0 max-h-0"
        enter-to-class="opacity-100 max-h-32"
        leave-active-class="transition-all duration-150 ease-in overflow-hidden"
        leave-from-class="opacity-100 max-h-32"
        leave-to-class="opacity-0 max-h-0"
      >
        <div
          v-if="selectedDay"
          class="flex items-center gap-3 flex-wrap rounded-lg bg-white/40 dark:bg-white/5 border border-gray-200/70 dark:border-gray-700/70 px-4 py-3"
        >
          <div class="flex items-center gap-2 shrink-0">
            <UIcon name="i-lucide-calendar-days" class="w-4 h-4 text-muted" />
            <span class="text-sm font-medium">{{ formatDate(selectedDay) }}</span>
            <UBadge color="neutral" variant="subtle" size="sm">
              {{ selectedDay.count }} total
            </UBadge>
          </div>
          <div class="w-px h-4 bg-gray-300/60 dark:bg-gray-600/60 shrink-0" />
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

      <!-- Legend -->
      <div class="flex items-center gap-2 justify-end">
        <span class="text-[10px] text-muted">Less</span>
        <div
          v-for="intensity in [0, 1, 2, 3, 4] as const"
          :key="intensity"
          class="w-3 h-3 rounded-sm"
          :class="
            CELL_COLORS[selectedOperation === null ? 'all' : String(selectedOperation)]?.[intensity]
          "
        />
        <span class="text-[10px] text-muted">More</span>
      </div>
    </div>
  </div>

  <!-- Shared tooltip (teleported to body, single instance) -->
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
        class="fixed z-50 pointer-events-none px-2 py-1 rounded-md text-xs font-medium bg-gray-900/90 dark:bg-gray-100/90 text-white dark:text-gray-900 backdrop-blur-sm shadow-md whitespace-nowrap"
        :style="{ left: `${tooltipX + 12}px`, top: `${tooltipY - 28}px` }"
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
</style>
