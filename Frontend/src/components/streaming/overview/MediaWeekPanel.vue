<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import { isAuthError } from "@/api/shuffle";
import { type WeekDay, buildWeekDays } from "@/features/media/overview";
import { homeTopSongsPeriod } from "@/features/media/top-songs";
import { mediaOverviewWeek } from "@/queries/media-overview";
import { formatCount } from "@/utils/count";
import { formatDuration } from "@/utils/date-formatters";

import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();

const BAR_MAX_PX = 88;
const GHOST_HEIGHTS = [30, 52, 24, 64, 40, 76, 20];

const { from, to } = homeTopSongsPeriod();

const query = useQuery(() => mediaOverviewWeek({ userId, session, from, to }));

const timeline = computed(() => query.data.value);
const days = computed(() => buildWeekDays(from, timeline.value?.days ?? []));
const isQuiet = computed(() => (timeline.value?.totalSeconds ?? 0) === 0);

const songsPlayed = (count: number) => `${formatCount(count, "song")} played`;

const summary = computed(() => {
  const data = timeline.value;
  if (!data) return "";

  return `${songsPlayed(data.qualifiedPlayCount)} · active ${data.activeDays} of 7 days`;
});

const peak = computed(() => Math.max(1, ...days.value.map((day) => day.seconds)));

const barHeight = (seconds: number) =>
  `${Math.max(6, Math.round((seconds / peak.value) * BAR_MAX_PX))}px`;

// Hover previews a day, a click or tap pins it; otherwise the busiest day is described.
const hoveredDate = ref<string | null>(null);
const selectedDate = ref<string | null>(null);

const onDayEnter = (event: PointerEvent, day: WeekDay) => {
  if (event.pointerType === "mouse") hoveredDate.value = day.date;
};

const onDayFocus = (event: FocusEvent, day: WeekDay) => {
  if ((event.target as HTMLElement).matches(":focus-visible")) hoveredDate.value = day.date;
};

const onDayLeave = () => {
  hoveredDate.value = null;
};

const toggleDay = (day: WeekDay) => {
  if (selectedDate.value === day.date) {
    selectedDate.value = null;

    return;
  }

  selectedDate.value = day.date;
};

const busiestDay = computed(() =>
  days.value.reduce((best, day) => {
    if (day.seconds > best.seconds) return day;

    return best;
  }, days.value[0]!),
);

const focusedDay = computed(() => {
  const date = hoveredDate.value ?? selectedDate.value;
  if (!date) return null;

  return days.value.find((day) => day.date === date) ?? null;
});

const detailDay = computed(() => focusedDay.value ?? busiestDay.value);

const detailHeading = computed(() => {
  if (focusedDay.value) return focusedDay.value.longLabel;

  return `Busiest day · ${busiestDay.value.longLabel}`;
});

const dayStats = (day: WeekDay): string => {
  if (day.seconds === 0) return "No listening";

  return `${formatDuration(day.seconds, { largestUnit: "hour" })} · ${songsPlayed(day.plays)}`;
};

const dayAriaLabel = (day: WeekDay): string => `${day.longLabel}, ${dayStats(day)}`;

const barClass = (day: WeekDay): string => {
  if (day.date === selectedDate.value) return "bg-primary";
  if (day.date === hoveredDate.value) return "bg-gray-500 dark:bg-gray-400";
  if (day.isToday) return "bg-gray-900 dark:bg-gray-100";

  return "bg-gray-300 dark:bg-gray-600";
};

const dayLabelClass = (day: WeekDay): string => {
  if (day.date === selectedDate.value || day.isToday) return "text-gray-900 dark:text-gray-100";

  return "text-gray-600 dark:text-gray-400";
};
</script>

<template>
  <MediaOverviewSection
    title="This week"
    to="/stats"
    link-label="Your listening"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="false"
    empty-seed="empty-week"
    empty-title=""
    empty-description=""
    class="flex flex-col"
    @retry="query.refetch()"
  >
    <div class="flex flex-1 flex-col justify-between gap-4">
      <div>
        <p
          class="text-3xl font-bold tracking-tight"
          :class="isQuiet ? 'text-gray-500 dark:text-gray-500' : 'text-gray-900 dark:text-gray-100'"
        >
          {{ formatDuration(timeline?.totalSeconds ?? 0, { largestUnit: "hour" }) }}
        </p>

        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
          <template v-if="isQuiet">Play some music and your week takes shape here.</template>

          <template v-else>Music listened · {{ summary }}</template>
        </p>
      </div>

      <div v-if="isQuiet" aria-hidden="true" class="flex h-28 items-end gap-2">
        <div
          v-for="(day, index) in days"
          :key="day.date"
          class="flex h-28 flex-1 flex-col items-center justify-end gap-1.5"
        >
          <div
            class="w-full max-w-8 rounded-md border-[1.5px] border-dashed border-gray-300 dark:border-gray-600"
            :style="{ height: `${GHOST_HEIGHTS[index]}px` }"
          />

          <span class="text-xs font-semibold text-gray-600 dark:text-gray-400">
            {{ day.label }}
          </span>
        </div>
      </div>

      <div v-else class="flex flex-col gap-3">
        <div role="group" aria-label="Listening per day" class="flex gap-2">
          <button
            v-for="day in days"
            :key="day.date"
            type="button"
            class="group flex min-w-0 flex-1 flex-col items-center gap-1.5 rounded-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            :aria-label="dayAriaLabel(day)"
            :aria-pressed="selectedDate === day.date"
            @pointerenter="onDayEnter($event, day)"
            @pointerleave="onDayLeave"
            @focus="onDayFocus($event, day)"
            @blur="onDayLeave"
            @click="toggleDay(day)"
          >
            <span
              class="flex w-full items-end justify-center"
              :style="{ height: `${BAR_MAX_PX}px` }"
            >
              <span
                class="block w-full max-w-8 rounded-md transition-[height,background-color] duration-300 motion-reduce:transition-none"
                :class="barClass(day)"
                :style="{ height: barHeight(day.seconds) }"
              />
            </span>

            <span class="text-xs font-semibold" :class="dayLabelClass(day)">
              {{ day.label }}
            </span>
          </button>
        </div>

        <p class="flex min-w-0 items-baseline justify-between gap-3 text-sm">
          <span class="min-w-0 truncate font-medium text-gray-900 dark:text-gray-100">
            {{ detailHeading }}
          </span>
          <span class="shrink-0 tabular-nums text-gray-600 dark:text-gray-400">
            {{ dayStats(detailDay) }}
          </span>
        </p>
      </div>
    </div>
  </MediaOverviewSection>
</template>
