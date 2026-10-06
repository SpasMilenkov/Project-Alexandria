<template>
  <section
    class="overflow-hidden rounded-3xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
  >
    <div class="flex items-center justify-between gap-4 px-6 pt-6">
      <p class="text-xs tracking-[0.16em] uppercase" :style="{ color: inks[0] }">
        {{ kicker }}
      </p>

      <span class="text-xs tracking-widest text-gray-600 dark:text-gray-400">PERSONAL NOTES</span>
    </div>

    <WrappedReplayArt v-if="card.type === WrappedCardType.MostReplayed" :count="facts.count" />
    <WrappedReturnArt
      v-else-if="card.type === WrappedCardType.ReturningFavorite"
      :from="facts.from"
      :to="facts.to"
      :count="facts.count"
    />
    <WrappedCalendarArt
      v-else-if="card.type === WrappedCardType.Streak && from && to"
      :facts="facts"
      :from="from"
      :to="to"
    />
    <WrappedWaveformArt v-else-if="card.type === WrappedCardType.BusiestDay" :facts="facts" />
    <WrappedArt v-else :card="card" :seed="seedId" />

    <div class="border-t border-gray-200/70 dark:border-gray-700/70 p-6 md:p-8">
      <h2
        class="max-w-[26ch] text-3xl leading-tight font-black tracking-tight text-gray-900 dark:text-gray-100 md:text-4xl"
      >
        {{ card.headline }}
      </h2>

      <p
        v-if="card.subline"
        class="mt-4 max-w-prose text-base leading-relaxed text-gray-600 dark:text-gray-400"
      >
        {{ card.subline }}
      </p>

      <div v-if="card.entries.length" class="mt-6 grid gap-4 sm:grid-cols-2">
        <div
          v-for="entry in card.entries"
          :key="`${entry.entityId}-${entry.rank}`"
          class="border-l-2 pl-4"
          :style="{ borderColor: inks[(entry.rank - 1) % 3] }"
        >
          <p v-if="entry.subtitle" class="mb-1 text-xs text-gray-600 dark:text-gray-400">
            {{ entry.subtitle }}
          </p>

          <RouterLink
            v-if="trackEntries && entry.entityId"
            :to="{ name: 'track-details', params: { fileId: entry.entityId } }"
            class="rounded font-semibold break-words text-gray-900 dark:text-gray-100 hover:underline focus-visible:outline-2 focus-visible:outline-primary"
            >{{ entry.title }}</RouterLink
          >
          <p v-else class="font-semibold break-words text-gray-900 dark:text-gray-100">
            {{ entry.title }}
          </p>

          <p v-if="entry.artist" class="mt-1 text-sm text-gray-600 dark:text-gray-400">
            {{ entry.artist }}
          </p>
        </div>
      </div>

      <WrappedStoryNotes class="mt-4" :card="card" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { WrappedCardResponse } from "@/api/stats";

import WrappedArt from "@/components/streaming/wrapped/WrappedArt.vue";
import WrappedCalendarArt from "@/components/streaming/wrapped/WrappedCalendarArt.vue";
import WrappedReplayArt from "@/components/streaming/wrapped/WrappedReplayArt.vue";
import WrappedReturnArt from "@/components/streaming/wrapped/WrappedReturnArt.vue";
import WrappedStoryNotes from "@/components/streaming/wrapped/WrappedStoryNotes.vue";
import WrappedWaveformArt from "@/components/streaming/wrapped/WrappedWaveformArt.vue";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { factsFor, wrappedInks } from "@/utils/wrapped-art.utils";

const props = defineProps<{
  card: WrappedCardResponse;
  seedId: string;
  from?: string;
  to?: string;
}>();

const facts = computed(() => factsFor(props.card));

const inks = computed(() => wrappedInks(props.card));

const trackEntries = computed(() =>
  [
    WrappedCardType.MostReplayed,
    WrappedCardType.RetainedDiscovery,
    WrappedCardType.ReturningFavorite,
  ].includes(props.card.type),
);

const kicker = computed(() => {
  const labels: Partial<Record<WrappedCardType, string>> = {
    [WrappedCardType.Persona]: "Your rhythm",
    [WrappedCardType.Exploration]: "Familiar & new",
    [WrappedCardType.RetainedDiscovery]: "A discovery that stayed",
    [WrappedCardType.Chapters]: "Changing seasons",
    [WrappedCardType.ReturningFavorite]: "The return",
    [WrappedCardType.Streak]: "Day after day",
    [WrappedCardType.BusiestDay]: "A standout moment",
    [WrappedCardType.MostReplayed]: "One more time",
    [WrappedCardType.NewArtists]: "New voices",
    [WrappedCardType.Discoveries]: "First encounters",
  };

  return labels[props.card.type as WrappedCardType] ?? "A moment from your listening";
});
</script>
