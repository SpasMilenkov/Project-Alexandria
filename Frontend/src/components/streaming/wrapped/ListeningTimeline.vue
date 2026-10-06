<template>
  <section aria-label="Your listening timeline">
    <header class="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
      <p class="text-sm text-gray-600 dark:text-gray-400">{{ rangeLabel }}</p>

      <div class="flex shrink-0 items-center gap-2" role="group" aria-label="Timeline range">
        <UButton
          icon="i-mdi-chevron-left"
          color="neutral"
          variant="ghost"
          square
          justify-center
          aria-label="Previous 90 days"
          @click="shift(-1)"
        />

        <UButton
          icon="i-mdi-chevron-right"
          color="neutral"
          variant="ghost"
          square
          justify-center
          aria-label="Next 90 days"
          :disabled="atPresent"
          @click="shift(1)"
        />
      </div>
    </header>

    <div
      v-if="isPending || authWaiting"
      class="flex justify-center py-16"
      role="status"
      aria-label="Loading your timeline"
    >
      <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-400 dark:text-gray-600" />
    </div>
    <div v-else-if="visibleError" class="frosted-glass glass-surface rounded-3xl p-8 text-center">
      <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load your timeline</p>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Something went wrong building your day-by-day history.
      </p>

      <UButton class="mt-4" variant="outline" color="neutral" @click="refetch()">Try again</UButton>
    </div>
    <div
      v-else-if="!data?.days.length"
      class="frosted-glass glass-surface flex flex-col items-center rounded-3xl px-4 py-16 text-center"
    >
      <UIcon name="mdi:music-note" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

      <h2 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">
        No listening in this range
      </h2>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Play some music, or step back to an earlier stretch.
      </p>

      <UButton class="mt-6" to="/streaming/music" color="primary">Find your soundtrack</UButton>
    </div>
    <div v-else>
      <div class="frosted-glass glass-surface rounded-3xl p-4 md:p-6">
        <p class="mb-6 text-sm text-gray-600 tabular-nums dark:text-gray-400">{{ summaryLabel }}</p>

        <TimelineCalendar
          :days="data.days"
          :from="from"
          :to="to"
          :model-value="selectedDate"
          @update:model-value="openDay"
        />

        <p class="mt-6 text-xs text-gray-600 dark:text-gray-400">
          Deeper color, more music. Empty dates have no listens in Alexandria.
        </p>
      </div>
    </div>

    <UDrawer
      v-model:open="drawerOpen"
      :title="dayTitle"
      description="Day drill-down"
      :direction="isMobile ? 'bottom' : 'right'"
      :ui="{ content: glassDrawerContent }"
    >
      <template #body>
        <div v-if="selectedDay" class="space-y-4 p-1">
          <p class="text-sm text-gray-600 tabular-nums dark:text-gray-400">
            {{ formatWrappedDuration(selectedDay.seconds) }} · {{ selectedDay.plays }} plays
          </p>

          <ul
            v-if="selectedDay.top.length"
            class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
          >
            <li
              v-for="track in selectedDay.top"
              :key="track.title"
              class="flex items-center gap-4 py-3"
            >
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                  {{ track.title }}
                </p>

                <p v-if="track.artist" class="truncate text-xs text-gray-500 dark:text-gray-500">
                  {{ track.artist }}
                </p>
              </div>

              <span class="shrink-0 text-xs text-gray-500 tabular-nums dark:text-gray-500">
                {{ formatWrappedDuration(track.seconds) }}
              </span>
            </li>
          </ul>
          <p v-else class="text-sm text-gray-600 dark:text-gray-400">No top tracks for this day.</p>
        </div>
      </template>
    </UDrawer>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { isAxiosError } from "axios";
import { computed, onDeactivated, ref } from "vue";

import TimelineCalendar from "@/components/streaming/wrapped/TimelineCalendar.vue";
import { getTimeline } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { glassDrawerContent } from "@/utils/modalUi";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { dateLabel } from "@/utils/wrapped-story.utils";

const DAY_MS = 86400000;
const WINDOW_DAYS = 90;

const endOfToday = (): number => {
  const now = new Date();

  return Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()) + DAY_MS;
};

const presentEnd = endOfToday();

const auth = useAuthStore();

const userId = computed(() => auth.user?.user.id);

const isMobile = useMediaQuery("(max-width: 767px)");

const from = ref(new Date(presentEnd - WINDOW_DAYS * DAY_MS).toISOString());
const to = ref(new Date(presentEnd).toISOString());
const selectedDate = ref<string | null>(null);
const drawerOpen = ref(false);

onDeactivated(() => {
  drawerOpen.value = false;
});

const atPresent = computed(() => new Date(to.value).getTime() >= presentEnd);

const { data, status, error, refetch } = useQuery(() =>
  // Omit the end boundary at the present so a client clock running ahead of the
  // server cannot push `to` into the future and trip range validation.
  getTimeline({
    from: from.value,
    to: atPresent.value ? undefined : to.value,
    userId: userId.value,
  }),
);

const isPending = computed(() => status.value === "pending");

const isAuthError = (err: unknown): boolean => {
  if (isAxiosError(err)) return err.response?.status === 401 || err.response?.status === 403;
  if (!err || typeof err !== "object") return false;

  const record = err as Record<string, unknown>;

  return record.status === 401 || record.status === 403;
};

const authWaiting = computed(() => !userId.value || isAuthError(error.value));

const visibleError = computed(() => error.value && !isAuthError(error.value));

const rangeLabel = computed(() => {
  const start = new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(new Date(from.value));

  const end = new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(to.value));

  return `${start} to ${end}`;
});

const summaryLabel = computed(() => {
  if (!data.value) return "";

  return `${formatWrappedDuration(data.value.totalSeconds)} · ${data.value.qualifiedPlayCount} plays · ${data.value.activeDays} active days`;
});

const selectedDay = computed(
  () => data.value?.days.find((day) => day.date === selectedDate.value) ?? null,
);

const dayTitle = computed(() =>
  selectedDate.value ? dateLabel(selectedDate.value) : "Listening day",
);

const shift = (steps: number) => {
  const nextTo = new Date(to.value).getTime() + steps * WINDOW_DAYS * DAY_MS;
  const clampedTo = Math.min(nextTo, presentEnd);

  to.value = new Date(clampedTo).toISOString();
  from.value = new Date(clampedTo - WINDOW_DAYS * DAY_MS).toISOString();
  selectedDate.value = null;
  drawerOpen.value = false;
};

const openDay = (date: string) => {
  selectedDate.value = date;
  drawerOpen.value = true;
};
</script>
