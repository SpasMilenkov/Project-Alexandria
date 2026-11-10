<template>
  <section aria-label="Your listening timeline" :style="paletteStyle">
    <header class="mb-4 flex flex-wrap items-center justify-between gap-4">
      <div>
        <h2 class="text-xl font-bold text-gray-900 dark:text-gray-100">Listening timeline</h2>
        <p class="mt-1 text-sm text-gray-600 tabular-nums dark:text-gray-400">{{ year }}</p>
      </div>

      <div class="flex shrink-0 items-center gap-2" role="group" aria-label="Timeline year">
        <UButton
          icon="i-mdi-chevron-left"
          color="neutral"
          variant="ghost"
          square
          class="justify-center"
          aria-label="Previous year"
          :disabled="year <= MIN_WRAPPED_YEAR"
          @click="shift(-1)"
        />

        <UButton
          icon="i-mdi-chevron-right"
          color="neutral"
          variant="ghost"
          square
          class="justify-center"
          aria-label="Next year"
          :disabled="atPresent"
          @click="shift(1)"
        />
      </div>
    </header>

    <div
      class="frosted-glass glass-surface rounded-3xl border border-gray-200/70 p-4 md:p-6 dark:border-gray-700/70"
    >
      <div
        v-if="isPending || authWaiting"
        class="flex h-40 items-center justify-center"
        role="status"
        aria-label="Loading your timeline"
      >
        <BlockSpinner :size="24" class="text-gray-500" />
      </div>

      <div
        v-else-if="visibleError"
        class="flex h-40 flex-col items-center justify-center text-center"
      >
        <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load your timeline</p>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Something went wrong building your day-by-day history.
        </p>

        <UButton class="mt-4" variant="outline" color="neutral" @click="refetch()"
          >Try again</UButton
        >
      </div>

      <div
        v-else-if="!data?.days.length"
        class="flex h-40 flex-col items-center justify-center text-center"
      >
        <UIcon name="mdi:music-note" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

        <h3 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">
          No listening in {{ year }}
        </h3>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Play some music, or step back to an earlier year.
        </p>
      </div>

      <div v-else class="flex flex-col gap-5">
        <dl class="flex flex-wrap gap-x-10 gap-y-3" aria-label="Year highlights">
          <div class="min-w-0">
            <dt class="text-xs text-gray-600 dark:text-gray-400">Listening days</dt>
            <dd class="mt-1 text-xl font-bold text-gray-900 tabular-nums dark:text-gray-100">
              {{ formatNumber(data.activeDays) }}
            </dd>
          </div>

          <div class="min-w-0">
            <dt class="text-xs text-gray-600 dark:text-gray-400">Longest streak</dt>
            <dd class="mt-1 text-xl font-bold text-gray-900 tabular-nums dark:text-gray-100">
              {{ streakLabel }}
            </dd>
          </div>

          <div v-if="busiest" class="min-w-0">
            <dt class="text-xs text-gray-600 dark:text-gray-400">Busiest day</dt>
            <dd class="mt-1 flex items-baseline gap-2">
              <button
                type="button"
                class="text-left text-xl font-bold text-gray-900 tabular-nums underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
                :aria-label="`Busiest day, ${dateLabel(busiest.date)}, ${formatWrappedDuration(busiest.seconds)}`"
                @click="selectDay(busiest.date)"
              >
                {{ shortDate(busiest.date) }}
              </button>
              <span class="text-xs text-gray-600 tabular-nums dark:text-gray-400">
                {{ formatWrappedDuration(busiest.seconds) }}
              </span>
            </dd>
          </div>
        </dl>

        <div class="min-w-0">
          <TimelineHeatmap
            :days="data.days"
            :year="year"
            :today="today"
            :model-value="activeDate"
            @update:model-value="selectDay"
          />

          <div
            class="mt-3 flex items-center justify-end gap-1.5 text-[10px] text-gray-600 dark:text-gray-400"
            aria-hidden="true"
          >
            <span class="mr-1">Less</span>
            <span class="size-2.5 rounded-[2px] bg-gray-500/15" />
            <span
              v-for="mix in [30, 50, 75, 100]"
              :key="mix"
              class="size-2.5 rounded-[2px]"
              :style="{
                backgroundColor: `color-mix(in srgb, var(--wrapped-blue) ${mix}%, transparent)`,
              }"
            />
            <span class="ml-1">More</span>
          </div>
        </div>

        <section
          v-if="activeDate"
          aria-label="Selected listening day"
          aria-live="polite"
          class="grid min-h-40 gap-4 border-t border-gray-200/70 pt-5 md:grid-cols-[14rem_minmax(0,1fr)] dark:border-gray-700/70"
        >
          <div class="min-w-0">
            <h3 class="font-semibold text-gray-900 dark:text-gray-100">
              {{ dateLabel(activeDate) }}
            </h3>

            <p class="mt-1 text-sm text-gray-600 tabular-nums dark:text-gray-400">
              {{ activeSummary }}
            </p>
          </div>

          <ol
            v-if="activeDay?.top.length"
            class="divide-y divide-gray-200/60 dark:divide-gray-700/60"
          >
            <li
              v-for="(track, index) in activeDay.top"
              :key="`${index}-${track.title}`"
              class="flex items-center gap-4 py-2 first:pt-0"
            >
              <span
                class="w-4 shrink-0 text-center text-sm font-semibold text-gray-600 tabular-nums dark:text-gray-400"
                >{{ index + 1 }}</span
              >

              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                  {{ track.title }}
                </p>

                <p v-if="track.artist" class="truncate text-xs text-gray-600 dark:text-gray-400">
                  {{ track.artist }}
                </p>
              </div>

              <span class="shrink-0 text-xs text-gray-600 tabular-nums dark:text-gray-400">
                {{ formatWrappedDuration(track.seconds) }}
              </span>
            </li>
          </ol>
          <p v-else class="text-sm text-gray-600 dark:text-gray-400">
            {{ activeDay ? "No top tracks for this day." : "No listening recorded on this day." }}
          </p>
        </section>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import TimelineHeatmap from "@/components/streaming/wrapped/listening/TimelineHeatmap.vue";
import { useWrappedPalette } from "@/composables/useWrappedPalette";
import { formatWrappedDuration } from "@/features/wrapped/art";
import { MIN_WRAPPED_YEAR } from "@/features/wrapped/display";
import {
  busiestDay,
  longestStreak,
  startOfUtcDay,
  yearBounds,
} from "@/features/wrapped/listening/year";
import { dateLabel } from "@/features/wrapped/story";
import { getTimeline } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { formatCount, formatNumber } from "@/utils/count";

const auth = useAuthStore();
const { paletteStyle } = useWrappedPalette();

const userId = computed(() => auth.user?.user.id);

const today = startOfUtcDay(new Date());
const presentYear = new Date(today).getUTCFullYear();

const year = ref(presentYear);
const selectedDate = ref<string | null>(null);

const atPresent = computed(() => year.value >= presentYear);

// The current year leaves its end open so a client clock running ahead of the server cannot
// push `to` into the future and trip range validation. It also shares the hero's cache entry.
const { data, status, error, refetch } = useQuery(() => {
  const bounds = yearBounds(year.value);

  return getTimeline({
    from: bounds.from,
    to: atPresent.value ? undefined : bounds.to,
    userId: userId.value,
  });
});

const isPending = computed(() => status.value === "pending");

const authWaiting = computed(() => !userId.value || isAuthError(error.value));

const visibleError = computed(() => error.value && !isAuthError(error.value));

const busiest = computed(() => busiestDay(data.value?.days ?? []));

const streakLabel = computed(() => {
  const streak = longestStreak(data.value?.days ?? []);

  return formatCount(streak, "day");
});

const shortDate = (date: string) =>
  new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", timeZone: "UTC" }).format(
    new Date(date),
  );

// Until a day is picked the panel shows the latest listening, so it never sits empty.
const latestListening = computed(() => {
  const listened = (data.value?.days ?? []).filter((day) => day.seconds > 0);

  return listened.reduce<string | null>(
    (latest, day) => (latest && latest > day.date ? latest : day.date),
    null,
  );
});

const activeDate = computed(() => selectedDate.value ?? latestListening.value);

const activeDay = computed(
  () => data.value?.days.find((day) => day.date === activeDate.value) ?? null,
);

const activeSummary = computed(() => {
  const day = activeDay.value;

  if (!day) return "Nothing played";

  return `${formatWrappedDuration(day.seconds)} · ${formatCount(day.plays, "play")}`;
});

const shift = (steps: number) => {
  year.value = Math.min(presentYear, Math.max(MIN_WRAPPED_YEAR, year.value + steps));
  selectedDate.value = null;
};

const selectDay = (date: string) => {
  selectedDate.value = date;
};
</script>
