<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import { mediaOverviewWeek } from "@/queries/media-overview";
import { homeTopSongsPeriod } from "@/utils/home-top-songs";
import { buildWeekDays, formatListeningTime } from "@/utils/media-overview";

import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();

const BAR_MAX_PX = 88;
const GHOST_HEIGHTS = [30, 52, 24, 64, 40, 76, 20];

const { from, to } = homeTopSongsPeriod();
const query = useQuery(() => mediaOverviewWeek({ userId, session, from, to }));
const timeline = computed(() => query.data.value);
const days = computed(() => buildWeekDays(from, timeline.value?.days ?? []));
const isQuiet = computed(() => (timeline.value?.totalSeconds ?? 0) === 0);
const peak = computed(() => Math.max(1, ...days.value.map((day) => day.seconds)));

const barHeight = (seconds: number) =>
  `${Math.max(6, Math.round((seconds / peak.value) * BAR_MAX_PX))}px`;

const summary = computed(() => {
  const data = timeline.value;
  if (!data) return "";

  const plays = `${data.qualifiedPlayCount.toLocaleString()} ${data.qualifiedPlayCount === 1 ? "play" : "plays"}`;

  return `${plays} · active ${data.activeDays} of 7 days`;
});

const chartLabel = computed(() =>
  days.value.map((day) => `${day.label} ${formatListeningTime(day.seconds)}`).join(", "),
);
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
          {{ formatListeningTime(timeline?.totalSeconds ?? 0) }}
        </p>

        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
          <template v-if="isQuiet">Play some music and your week takes shape here.</template>

          <template v-else>Music listened · {{ summary }}</template>
        </p>
      </div>

      <div
        :role="isQuiet ? undefined : 'img'"
        :aria-label="isQuiet ? undefined : `Listening per day: ${chartLabel}`"
        :aria-hidden="isQuiet ? 'true' : undefined"
        class="flex h-28 items-end gap-2"
      >
        <div
          v-for="(day, index) in days"
          :key="day.date"
          class="flex h-28 flex-1 flex-col items-center justify-end gap-1.5"
        >
          <div
            v-if="isQuiet"
            class="w-full max-w-8 rounded-md border-[1.5px] border-dashed border-gray-300 dark:border-gray-600"
            :style="{ height: `${GHOST_HEIGHTS[index]}px` }"
          />

          <div
            v-else
            class="w-full max-w-8 rounded-md transition-[height] duration-500 motion-reduce:transition-none"
            :class="day.isToday ? 'bg-gray-900 dark:bg-gray-100' : 'bg-gray-300 dark:bg-gray-600'"
            :style="{ height: barHeight(day.seconds) }"
          />

          <span
            class="text-xs font-semibold"
            :class="
              day.isToday ? 'text-gray-900 dark:text-gray-100' : 'text-gray-600 dark:text-gray-400'
            "
            >{{ day.label }}</span
          >
        </div>
      </div>
    </div>
  </MediaOverviewSection>
</template>
