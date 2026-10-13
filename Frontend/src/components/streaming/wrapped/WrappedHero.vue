<template>
  <section
    class="overflow-hidden rounded-3xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
  >
    <div class="flex items-center justify-between gap-4 px-6 pt-6 md:px-8 md:pt-8">
      <p class="text-xs font-medium tracking-[0.18em] text-gray-600 dark:text-gray-400 uppercase">
        {{ eyebrow }}
      </p>

      <span class="text-xs tracking-widest text-gray-600 dark:text-gray-400">SIDE A / 01</span>
    </div>

    <WrappedDailyLandscape v-if="month" :month="month" :series="dailySeries" :through="through" />
    <WrappedLandscape v-else :series="facts.series" />

    <div
      class="grid gap-6 border-t border-gray-200/70 dark:border-gray-700/70 p-6 md:grid-cols-[1.3fr_1fr] md:p-8"
    >
      <div>
        <h2
          class="max-w-[18ch] text-3xl leading-[1.06] font-black tracking-tight text-gray-900 dark:text-gray-100 md:text-5xl"
        >
          {{ card.headline }}
        </h2>

        <p class="mt-4 text-base leading-relaxed" :style="{ color: inks[2] }">
          {{ card.subline }}
        </p>
      </div>

      <div class="flex flex-col justify-end">
        <WrappedMilestone
          v-if="facts.comparison"
          :comparison="facts.comparison"
          :alternatives="facts.comparisonAlternatives"
          :ink="inks[0]!"
        />

        <WrappedStoryNotes class="mt-4" :card="card" />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { WrappedCardResponse, WrappedPeriodPoint } from "@/api/stats";

import WrappedDailyLandscape from "@/components/streaming/wrapped/WrappedDailyLandscape.vue";
import WrappedLandscape from "@/components/streaming/wrapped/WrappedLandscape.vue";
import WrappedMilestone from "@/components/streaming/wrapped/WrappedMilestone.vue";
import WrappedStoryNotes from "@/components/streaming/wrapped/WrappedStoryNotes.vue";
import { factsFor, wrappedInks } from "@/utils/wrapped-art.utils";

const {
  card,
  month = undefined,
  dailySeries = [],
  through = "",
} = defineProps<{
  card: WrappedCardResponse;
  eyebrow: string;
  month?: string;
  dailySeries?: WrappedPeriodPoint[];
  through?: string;
}>();

const facts = computed(() => factsFor(card));

const inks = computed(() => wrappedInks(card));
</script>
