<template>
  <div class="wrapped-experience flex flex-col gap-6" :style="paletteStyle">
    <WrappedHero
      v-if="sections.hero"
      :card="sections.hero"
      :eyebrow="`${year} · a soundtrack only you could make`"
    />

    <dl
      v-if="response.summary"
      class="grid grid-cols-2 gap-4 rounded-3xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface p-6 sm:grid-cols-4 md:p-8"
    >
      <div v-for="stat in summary" :key="stat.label">
        <dt class="text-xs tracking-wide text-gray-600 dark:text-gray-400">{{ stat.label }}</dt>
        <dd
          class="mt-2 text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100 md:text-3xl"
        >
          {{ stat.value }}
        </dd>
      </div>
    </dl>

    <div v-if="sections.artists || sections.songs" class="grid items-start gap-6 md:grid-cols-2">
      <WrappedCountdown v-if="sections.artists" :card="sections.artists" />
      <WrappedCountdown v-if="sections.songs" :card="sections.songs" />
    </div>

    <template v-for="card in sections.spotlight" :key="card.id ?? card.type">
      <WrappedTimeOfDay v-if="card.type === WrappedCardType.Persona" :card="card" />
      <WrappedBookends
        v-else-if="card.type === WrappedCardType.Bookends"
        :card="card"
        :from="response.from"
        :to="response.to"
      />
      <WrappedMoment v-else :card="card" :seed-id="seed" :from="response.from" :to="response.to" />
    </template>

    <p class="px-2 pb-4 text-center text-xs leading-relaxed text-gray-600 dark:text-gray-400">
      Made from the music you've played in Alexandria.
      <span v-if="response.summary && !response.summary.hasPriorHistory"
        >This is the beginning of your Alexandria listening story.</span
      >
      <span v-else-if="response.summary && response.summary.knownArtistShare < 0.95"
        >Some tracks don't have artist names yet, so your artist list may be incomplete.</span
      >
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, provide } from "vue";

import type { WrappedDeckResponse } from "@/api/stats";

import WrappedBookends from "@/components/streaming/wrapped/WrappedBookends.vue";
import WrappedCountdown from "@/components/streaming/wrapped/WrappedCountdown.vue";
import WrappedHero from "@/components/streaming/wrapped/WrappedHero.vue";
import WrappedMoment from "@/components/streaming/wrapped/WrappedMoment.vue";
import WrappedTimeOfDay from "@/components/streaming/wrapped/WrappedTimeOfDay.vue";
import { useTheme } from "@/composables/useTheme";
import { wrappedPaletteKey } from "@/composables/useWrappedPalette";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { type WrappedPalette, wrappedCssColors } from "@/utils/wrapped-palette.utils";
import { buildWrappedSections } from "@/utils/wrapped-sections.utils";

const props = defineProps<{
  response: WrappedDeckResponse;
  year: number;
  palette?: WrappedPalette | null;
}>();

const { isDark } = useTheme();

const palette = computed(() => props.palette ?? null);

provide(wrappedPaletteKey, palette);

const paletteStyle = computed(() => wrappedCssColors(isDark.value, palette.value));

const sections = computed(() => buildWrappedSections(props.response.deck.cards));

const seed = computed(() => props.response.visualIdentity ?? String(props.year));

const summary = computed(() => [
  {
    label: "Time with your music",
    value: formatWrappedDuration(props.response.summary?.seconds ?? 0),
  },
  { label: "Different tracks", value: props.response.summary?.tracks ?? 0 },
  { label: "Different artists", value: props.response.summary?.artists ?? 0 },
  { label: "Listening days", value: props.response.summary?.activeDays ?? 0 },
]);
</script>
