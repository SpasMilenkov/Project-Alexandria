<script setup lang="ts">
import { useIntervalFn, useNow } from "@vueuse/core";
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import { HomeDailyCoverStyle } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeCalendarDate, homeLocalDay } from "@/utils/home-general";

import HomeDailyCoverArt from "./HomeDailyCoverArt.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();
const home = useHomeStore();
const now = useNow({ scheduler: (cb) => useIntervalFn(cb, 30_000) });

const day = computed(() => homeLocalDay(now.value));

const date = computed(() => homeCalendarDate(day.value)!);

const label = computed(() =>
  date.value.toLocaleDateString(undefined, { weekday: "long", month: "long", day: "numeric" }),
);

const weekday = computed(() => date.value.toLocaleDateString(undefined, { weekday: "long" }));

const month = computed(() =>
  date.value.toLocaleDateString(undefined, { month: "long", year: "numeric" }),
);
</script>

<template>
  <div
    class="daily-cover relative isolate flex min-h-0 flex-1 flex-col overflow-hidden"
    :data-cover-day="day"
    :data-cover-style="widget.options.dailyCoverStyle"
  >
    <div class="daily-cover-art relative min-h-0 flex-1 overflow-hidden">
      <HomeDailyCoverArt
        class="absolute inset-0"
        :day="day"
        :cover-style="widget.options.dailyCoverStyle"
      />
    </div>

    <div
      v-if="widget.options.dailyCoverStyle === HomeDailyCoverStyle.DatePoster"
      class="daily-band flex shrink-0 items-center gap-2 border-t border-gray-200/70 px-4 py-2 text-gray-900 dark:border-gray-700/70 dark:text-gray-100"
    >
      <b class="daily-number tabular-nums">{{ date.getDate() }}</b>
      <div class="min-w-0">
        <p class="truncate text-sm font-semibold">{{ weekday }}</p>
        <p class="daily-month truncate text-xs text-gray-600 dark:text-gray-400">{{ month }}</p>
      </div>
    </div>

    <div
      v-else
      class="daily-caption pointer-events-none absolute inset-x-0 bottom-0 flex flex-wrap items-end justify-between gap-2 px-4 pb-3 pt-8 text-white"
    >
      <b v-if="!(widget.showTitle ?? home.showWidgetTitles)" class="text-sm">Daily cover</b>
      <span class="daily-date text-xs text-gray-200 dark:text-gray-200">{{ label }}</span>
    </div>
  </div>
</template>

<style scoped>
.daily-cover {
  container: daily-cover / size;
}

.daily-caption {
  background: linear-gradient(transparent, rgb(0 0 0 / 80%));
}

.daily-band {
  background: var(--ui-bg);
}

.daily-number {
  font-size: clamp(1.25rem, 18cqmin, 2.5rem);
  line-height: 1;
}

@container daily-cover (max-height: 150px) {
  .daily-date,
  .daily-month {
    display: none;
  }
  .daily-caption {
    padding: 1rem 0.5rem 0.5rem;
  }
  .daily-band {
    padding-inline: 0.5rem;
  }
}
</style>
