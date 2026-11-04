<template>
  <figure class="px-6 pt-6 pb-4 md:px-8">
    <figcaption
      class="mb-4 flex flex-wrap items-baseline justify-between gap-2 text-sm"
      aria-live="polite"
    >
      <span class="font-semibold text-gray-900 dark:text-gray-100">{{ selectedLabel }}</span>

      <span class="text-gray-600 dark:text-gray-400"
        >{{ current ? formatWrappedDuration(current.seconds) : "No recorded listening"
        }}<span v-if="current"> · {{ current.plays }} plays</span></span
      >
    </figcaption>

    <div
      class="flex h-48 gap-1 md:h-56"
      role="group"
      aria-label="Monthly listening landscape. Choose a month for its total."
    >
      <button
        v-for="peak in peaks"
        :key="peak.month.date"
        type="button"
        class="group relative flex min-w-0 flex-1 flex-col justify-end rounded-t-lg focus-visible:outline-2 focus-visible:outline-primary"
        :aria-label="`${monthName(peak.month)}: ${formatWrappedDuration(peak.month.seconds)}, ${peak.month.plays} plays`"
        :aria-pressed="current?.date === peak.month.date"
        @click="selected = peak.month.date"
        @focus="selected = peak.month.date"
      >
        <svg
          viewBox="0 0 100 180"
          preserveAspectRatio="none"
          class="h-full w-full"
          aria-hidden="true"
        >
          <path
            :d="peak.path"
            :fill="
              peak.month.date === busiest?.date ? 'var(--wrapped-teal)' : 'var(--wrapped-blue)'
            "
          />

          <path
            v-if="peak.month.seconds > 0"
            :d="peak.path"
            fill="none"
            stroke="var(--wrapped-cutout)"
            stroke-width="1"
          />

          <path d="M 46 174 H 54" stroke="var(--wrapped-line)" stroke-width="2" />
        </svg>
        <span
          class="py-2 text-[10px] text-gray-600 sm:text-xs dark:text-gray-400"
          :class="{ 'font-bold underline underline-offset-4': current?.date === peak.month.date }"
          >{{ monthLabel(peak.month) }}</span
        >
      </button>
    </div>

    <p class="mt-2 text-xs text-gray-600 dark:text-gray-400">
      The taller the hill, the more music filled that month. Empty months stay quiet.
    </p>
  </figure>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { WrappedPeriodPoint } from "@/api/stats";

import { formatWrappedDuration, monthLabel } from "@/utils/wrapped-art.utils";
import { monthlyLandscape } from "@/utils/wrapped-ranking.utils";

const props = defineProps<{ series: WrappedPeriodPoint[] }>();
const selected = ref<string | null>(null);

watch(
  () => props.series,
  () => {
    selected.value = null;
  },
);

const peaks = computed(() => monthlyLandscape(props.series));

const busiest = computed(
  () =>
    [...props.series].filter((month) => month.seconds > 0).sort((a, b) => b.seconds - a.seconds)[0],
);

const current = computed(
  () => props.series.find((month) => month.date === selected.value) ?? busiest.value,
);

const monthName = (month: WrappedPeriodPoint) =>
  new Intl.DateTimeFormat(undefined, { month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(month.date),
  );

const selectedLabel = computed(() => {
  if (!current.value) return "Your listening, month by month";

  const label = monthName(current.value);

  if (current.value.date === busiest.value?.date) return `${label} · your busiest month`;

  return label;
});
</script>
