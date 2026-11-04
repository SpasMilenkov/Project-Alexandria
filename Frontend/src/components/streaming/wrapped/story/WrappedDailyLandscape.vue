<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedPeriodPoint } from "@/api/stats";

import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { type WrappedDayBar, wrappedDailyLandscape } from "@/utils/wrapped-daily-landscape";

const { month, through, series } = defineProps<{
  month: string;
  through: string;
  series: WrappedPeriodPoint[];
}>();

const chart = ref<HTMLElement | null>(null);
const selected = ref<string | null>(null);

watch(
  () => month,
  () => {
    selected.value = null;
  },
);

const days = computed(() => wrappedDailyLandscape(month, through, series));

const observed = computed(() => days.value.filter((day) => !day.future));

const busiest = computed(
  () =>
    [...observed.value].filter((day) => day.seconds > 0).sort((a, b) => b.seconds - a.seconds)[0],
);

const current = computed(
  () =>
    observed.value.find((day) => day.date === selected.value) ?? busiest.value ?? observed.value[0],
);

const currentIndex = computed(() =>
  observed.value.findIndex((day) => day.date === current.value?.date),
);

const dayName = (day: WrappedDayBar) =>
  new Intl.DateTimeFormat(undefined, {
    weekday: "long",
    month: "long",
    day: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${day.date}T00:00:00Z`));

const describe = (day: WrappedDayBar) => {
  const name = dayName(day);

  if (day.future) return `${name}: still ahead`;
  if (!day.seconds) return `${name}: no recorded listening`;

  return `${name}: ${formatWrappedDuration(day.seconds)}, ${day.plays} plays`;
};

const selectedLabel = computed(() => {
  const day = current.value;

  if (!day) return "Your listening, day by day";
  if (!busiest.value || day.seconds !== busiest.value.seconds) return dayName(day);

  const tied = observed.value.filter((point) => point.seconds === day.seconds).length > 1;
  const distinction = tied ? "one of your busiest days" : "your busiest day";

  return `${dayName(day)} · ${distinction}`;
});

const showTick = (day: WrappedDayBar) =>
  day.day === 1 || (day.day <= 25 && day.day % 5 === 0) || day.day === days.value.length;

const selectDay = (index: number, focus = false) => {
  const day = observed.value[index];

  if (!day) return;

  selected.value = day.date;

  if (focus) chart.value?.querySelector<HTMLButtonElement>(`[data-day="${day.date}"]`)?.focus();
};

const move = (event: KeyboardEvent) => {
  let index = currentIndex.value;

  if (event.key === "ArrowLeft") index--;
  else if (event.key === "ArrowRight") index++;
  else if (event.key === "Home") index = 0;
  else if (event.key === "End") index = observed.value.length - 1;
  else return;

  event.preventDefault();
  selectDay(index, true);
};
</script>

<template>
  <figure aria-label="Your month in music, day by day" class="px-6 pt-6 pb-4 md:px-8">
    <figcaption
      class="mb-4 flex min-h-16 flex-wrap items-baseline justify-between gap-2 text-sm"
      aria-live="polite"
    >
      <span class="font-semibold text-gray-900 dark:text-gray-100">{{ selectedLabel }}</span>

      <span v-if="current" class="text-gray-600 dark:text-gray-400">
        {{ formatWrappedDuration(current.seconds) }} · {{ current.plays }} plays
      </span>
    </figcaption>

    <template v-if="days.length">
      <div
        ref="chart"
        class="flex h-48 gap-0.5 md:h-56 sm:gap-1"
        role="group"
        aria-label="Daily listening skyline. Use left and right arrows to choose a day."
      >
        <button
          v-for="day in days"
          :key="day.date"
          type="button"
          :data-day="day.date"
          :data-future="day.future"
          :disabled="day.future"
          :tabindex="current?.date === day.date ? 0 : -1"
          :aria-label="describe(day)"
          :aria-pressed="current?.date === day.date"
          :title="describe(day)"
          class="daily-column relative flex min-w-0 flex-1 flex-col rounded-sm focus-visible:outline-2 focus-visible:outline-primary"
          :class="{ 'daily-column--selected': current?.date === day.date }"
          @click="selected = day.date"
          @focus="selected = day.date"
          @keydown="move"
        >
          <span class="flex min-h-0 flex-1 items-end pb-2" aria-hidden="true">
            <span
              v-if="day.future"
              class="daily-future h-1 w-full rounded-sm border border-dashed"
            />
            <span
              v-else-if="day.seconds > 0"
              class="daily-bar w-full rounded-t-sm"
              :class="{ 'daily-bar--busiest': day.seconds === busiest?.seconds }"
              :style="{ height: `${day.height}%` }"
            />
            <span v-else class="daily-quiet h-px w-full" />
          </span>

          <span
            class="h-6 shrink-0 text-[10px] leading-6 text-gray-600 sm:text-xs dark:text-gray-400"
            aria-hidden="true"
          >
            <template v-if="showTick(day)">{{ day.day }}</template>
          </span>
        </button>
      </div>

      <div class="mt-2 flex items-center justify-between gap-4">
        <p class="text-xs text-gray-600 dark:text-gray-400">
          Taller bars, more music.
          <template v-if="days.some((day) => day.future)">Dashed dates are still ahead.</template>
          <template v-else>Quiet days stay on the line.</template>
        </p>

        <div class="flex shrink-0 gap-2" role="group" aria-label="Inspect listening days">
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-chevron-left"
            aria-label="Previous listening day"
            title="Previous day"
            class="min-h-10 min-w-10 justify-center"
            :disabled="currentIndex <= 0"
            @click="selectDay(currentIndex - 1)"
          />
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-chevron-right"
            aria-label="Next listening day"
            title="Next day"
            class="min-h-10 min-w-10 justify-center"
            :disabled="currentIndex >= observed.length - 1"
            @click="selectDay(currentIndex + 1)"
          />
        </div>
      </div>
    </template>

    <p v-else class="py-4 text-sm text-gray-600 dark:text-gray-400">
      Daily listening isn't available for this recap.
    </p>
  </figure>
</template>

<style scoped>
.daily-bar {
  background: var(--wrapped-blue);
}

.daily-bar--busiest {
  background: var(--wrapped-teal);
}

.daily-quiet {
  background: var(--wrapped-line);
}

.daily-future {
  border-color: var(--wrapped-line);
}

.daily-column--selected::after {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 2px;
  background: var(--ui-text);
  content: "";
}
</style>
