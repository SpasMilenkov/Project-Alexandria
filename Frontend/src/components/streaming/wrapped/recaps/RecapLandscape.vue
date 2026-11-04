<template>
  <figure>
    <div
      class="flex gap-1"
      :class="compact ? 'h-24' : 'h-44'"
      role="group"
      :aria-label="`${year} listening by month. Choose a month for its total.`"
    >
      <button
        v-for="peak in peaks"
        :key="peak.month.date"
        type="button"
        class="group flex min-w-0 flex-1 flex-col rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        :aria-label="monthDescription(peak.month)"
        :aria-pressed="selected?.date === peak.month.date"
        @click="chosenDate = peak.month.date"
        @focus="chosenDate = peak.month.date"
      >
        <svg
          viewBox="0 0 100 180"
          preserveAspectRatio="none"
          class="min-h-0 w-full flex-1"
          aria-hidden="true"
        >
          <path
            v-if="peak.month.seconds > 0"
            :d="peak.path"
            :fill="
              peak.month.date === busiest?.date ? 'var(--wrapped-teal)' : 'var(--wrapped-purple)'
            "
            :fill-opacity="peak.month.date === selected?.date ? 1 : 0.65"
            class="transition-[fill-opacity] duration-200 motion-reduce:transition-none"
          />
          <path
            v-else
            d="M 40 166 H 60"
            stroke="var(--wrapped-line)"
            stroke-width="2"
            stroke-linecap="round"
          />
        </svg>
        <span
          class="pt-2 text-[10px] text-gray-600 dark:text-gray-400"
          :class="{
            'font-semibold underline underline-offset-4': peak.month.date === selected?.date,
          }"
          aria-hidden="true"
          >{{ monthName(peak.month).slice(0, 1) }}</span
        >
      </button>
    </div>

    <figcaption
      class="mt-4 min-h-8 text-xs text-gray-600 tabular-nums dark:text-gray-400"
      aria-live="polite"
    >
      <template v-if="selected">
        <span v-if="selected.date === busiest?.date">Busiest month: </span
        >{{ monthDescription(selected) }}
      </template>
      <template v-else>No recorded listening in this year.</template>
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedPeriodPoint } from "@/api/stats";

import { annualRecapMonths } from "@/utils/listening-recaps.utils";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { monthlyLandscape } from "@/utils/wrapped-ranking.utils";

const {
  year,
  series,
  compact = false,
  through = undefined,
} = defineProps<{
  year: number;
  series: WrappedPeriodPoint[];
  compact?: boolean;
  through?: string;
}>();

const chosenDate = ref<string | null>(null);

watch(
  () => series,
  () => {
    chosenDate.value = null;
  },
);

const months = computed(() => annualRecapMonths(series, year));

const peaks = computed(() => monthlyLandscape(months.value));

const busiest = computed(
  () =>
    [...months.value].filter((month) => month.seconds > 0).sort((a, b) => b.seconds - a.seconds)[0],
);

const selected = computed(
  () => months.value.find((month) => month.date === chosenDate.value) ?? busiest.value,
);

const monthName = (month: WrappedPeriodPoint) =>
  new Intl.DateTimeFormat(undefined, { month: "long", timeZone: "UTC" }).format(
    new Date(month.date),
  );

const monthDescription = (month: WrappedPeriodPoint) => {
  const name = monthName(month);

  if (through && Date.parse(month.date) > Date.parse(through)) return `${name}: still ahead`;
  if (!month.seconds) return `${name}: no recorded listening`;

  return `${name}: ${formatWrappedDuration(month.seconds)}, ${month.plays} plays`;
};
</script>
