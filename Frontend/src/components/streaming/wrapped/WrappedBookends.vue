<template>
  <section
    class="overflow-hidden rounded-3xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
  >
    <div class="flex items-center justify-between gap-4 px-6 pt-6">
      <p class="text-xs tracking-[0.16em] text-gray-600 dark:text-gray-400 uppercase">
        The closing notes
      </p>

      <span class="text-xs tracking-widest text-gray-600 dark:text-gray-400">SIDE B / END</span>
    </div>

    <WrappedVinylArt :facts="factsFor(card)" :from="from" :to="to" />

    <div class="border-t border-gray-200/70 dark:border-gray-700/70 p-6 md:p-8">
      <h2 class="text-2xl font-black text-gray-900 dark:text-gray-100">{{ card.headline }}</h2>

      <div class="mt-6 grid gap-6 sm:grid-cols-2">
        <div v-for="entry in card.entries" :key="entry.rank">
          <p class="mb-2 text-xs tracking-wider text-gray-600 dark:text-gray-400 uppercase">
            {{ entry.subtitle }}
          </p>

          <RouterLink
            v-if="entry.entityId"
            :to="{ name: 'track-details', params: { fileId: entry.entityId } }"
            class="rounded text-xl font-bold break-words text-gray-900 dark:text-gray-100 hover:underline focus-visible:outline-2 focus-visible:outline-primary"
            >{{ entry.title }}</RouterLink
          >
          <p v-else class="text-xl font-bold break-words text-gray-900 dark:text-gray-100">
            {{ entry.title }}
          </p>

          <p v-if="entry.artist" class="mt-1 text-sm text-gray-600 dark:text-gray-400">
            {{ entry.artist }}
          </p>
        </div>
      </div>

      <p
        v-if="span !== null && card.entries.length > 1"
        class="mt-6 text-sm text-gray-600 dark:text-gray-400"
      >
        {{ span }} {{ span === 1 ? "day" : "days" }} between these plays.
      </p>

      <WrappedStoryNotes class="mt-4" :card="card" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { WrappedCardResponse } from "@/api/stats";

import WrappedStoryNotes from "@/components/streaming/wrapped/WrappedStoryNotes.vue";
import WrappedVinylArt from "@/components/streaming/wrapped/WrappedVinylArt.vue";
import { factsFor } from "@/utils/wrapped-art.utils";

const props = defineProps<{
  card: WrappedCardResponse;
  from: string;
  to: string;
}>();

const span = computed(() => {
  const first = props.card.entries[0]?.date;
  const last = props.card.entries[props.card.entries.length - 1]?.date;

  if (!first || !last) return null;

  const a = Date.parse(first.slice(0, 10));
  const b = Date.parse(last.slice(0, 10));

  if (!Number.isFinite(a) || !Number.isFinite(b)) return null;

  return Math.floor((b - a) / 86400000);
});
</script>
