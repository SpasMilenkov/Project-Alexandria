<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { getWrapped } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { factsFor, formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { wrappedDailyLandscape } from "@/utils/wrapped-daily-landscape";
import { wrappedMonthLabel, wrappedMonthLocation, wrappedMonthRange } from "@/utils/wrapped-month";

const { month, now } = defineProps<{ month: string; now: Date }>();

const auth = useAuthStore();
const userId = computed(() => auth.user?.user.id);
const label = computed(() => wrappedMonthLabel(month));
const range = computed(() => wrappedMonthRange(month, now));

const { data, status, error, refetch } = useQuery(() =>
  getWrapped({ userId: userId.value, from: range.value.from, to: range.value.to }),
);

const authWaiting = computed(() => !userId.value || isAuthError(error.value));
const loading = computed(() => status.value === "pending" || authWaiting.value);
const failed = computed(() => Boolean(error.value) && !isAuthError(error.value));
const hasListening = computed(() => Boolean(data.value?.deck.cards.length));

const daily = computed(() => {
  const response = data.value;
  const bookends = response?.deck.cards.find((card) => card.type === WrappedCardType.Bookends);

  if (!response || !bookends) return [];

  return wrappedDailyLandscape(month, response.to, factsFor(bookends).series);
});

const maximum = computed(() => Math.max(0, ...daily.value.map((day) => day.seconds)));
const numberFormat = new Intl.NumberFormat();

const displayFacts = computed(() => {
  const summary = data.value?.summary;

  if (!summary) return [];

  return [
    { label: "Tracks", value: numberFormat.format(summary.tracks) },
    { label: "Artists", value: numberFormat.format(summary.artists) },
    { label: "Listening days", value: numberFormat.format(summary.activeDays) },
  ];
});

const favorites = computed(() => {
  const types = [
    { type: WrappedCardType.TopArtists, label: "Top artist" },
    { type: WrappedCardType.TopSongs, label: "Top song" },
  ];

  return types.flatMap(({ type, label: title }) => {
    const card = data.value?.deck.cards.find((item) => item.type === type);
    const leader = [...(card?.entries ?? [])].sort((a, b) => a.rank - b.rank)[0];

    return leader ? [{ label: title, value: leader.title }] : [];
  });
});
</script>

<template>
  <article
    class="frosted-glass glass-surface flex min-w-0 flex-col gap-6 rounded-3xl border border-gray-200/70 p-6 dark:border-gray-700/70"
    :aria-label="`${label} recap`"
  >
    <header class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <p class="text-xs font-semibold tracking-wide text-gray-600 dark:text-gray-400">
          {{ range.isFinal ? "Last month" : "This month" }}
        </p>
        <h2 class="mt-2 text-2xl font-black tracking-tight text-gray-900 dark:text-gray-100">
          {{ label }}
        </h2>
      </div>

      <UBadge color="neutral" variant="subtle" class="shrink-0">
        {{ range.isFinal ? "Complete" : "So far" }}
      </UBadge>
    </header>

    <div
      v-if="loading"
      class="flex min-h-80 flex-1 items-center justify-center"
      role="status"
      :aria-label="`Loading ${label} recap`"
    >
      <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-400 dark:text-gray-600" />
    </div>
    <div
      v-else-if="failed"
      class="flex min-h-80 flex-1 flex-col items-center justify-center text-center"
    >
      <h3 class="font-semibold text-gray-900 dark:text-gray-100">Could not load this month</h3>
      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">Try again to see {{ label }}.</p>

      <UButton class="mt-4" color="neutral" variant="outline" @click="refetch()">Try again</UButton>
    </div>
    <div
      v-else-if="!hasListening"
      class="flex min-h-80 flex-1 flex-col items-center justify-center text-center"
    >
      <UIcon name="mdi:music-note-outline" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
      <h3 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">No recorded listening</h3>
      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        {{
          range.isFinal
            ? "No music was recorded in Alexandria this month."
            : "Play some music to start this month's recap."
        }}
      </p>

      <UButton v-if="!range.isFinal" class="mt-6" to="/streaming/music" color="primary"
        >Browse music</UButton
      >
    </div>
    <template v-else>
      <div v-if="data?.summary">
        <p class="text-4xl font-black tracking-tight text-gray-900 tabular-nums dark:text-gray-100">
          {{ formatWrappedDuration(data.summary.seconds) }}
        </p>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          with your music · {{ numberFormat.format(data.summary.qualifiedPlayCount) }} plays
        </p>
      </div>

      <figure v-if="daily.length" :aria-label="`Daily listening in ${label}`">
        <div class="flex h-24 items-end gap-1" aria-hidden="true">
          <span
            v-for="day in daily"
            :key="day.date"
            class="min-w-0 flex-1 rounded-t-sm"
            :class="{
              'monthly-bar': !day.future && day.seconds > 0,
              'monthly-bar--busiest': day.seconds > 0 && day.seconds === maximum,
              'monthly-quiet h-px': !day.future && !day.seconds,
              'monthly-future h-1 border border-dashed': day.future,
            }"
            :style="day.seconds > 0 ? { height: `${day.height}%` } : undefined"
          />
        </div>

        <figcaption class="mt-2 flex justify-between text-xs text-gray-600 dark:text-gray-400">
          <span>1</span>
          <span>Day by day</span>
          <span>{{ daily.length }}</span>
        </figcaption>
      </figure>
      <p v-else class="flex h-32 items-center text-sm text-gray-600 dark:text-gray-400">
        Daily detail isn't available for this recap.
      </p>

      <dl
        v-if="displayFacts.length"
        class="grid grid-cols-3 gap-4 border-t border-gray-200/70 pt-4 dark:border-gray-700/70"
      >
        <div v-for="fact in displayFacts" :key="fact.label" class="min-w-0">
          <dt class="text-xs text-gray-600 dark:text-gray-400">{{ fact.label }}</dt>
          <dd class="mt-2 text-xl font-bold text-gray-900 tabular-nums dark:text-gray-100">
            {{ fact.value }}
          </dd>
        </div>
      </dl>

      <dl v-if="favorites.length" class="grid gap-4">
        <div v-for="favorite in favorites" :key="favorite.label" class="min-w-0">
          <dt class="text-xs text-gray-600 dark:text-gray-400">{{ favorite.label }}</dt>
          <dd class="mt-1 text-sm font-semibold break-words text-gray-900 dark:text-gray-100">
            {{ favorite.value }}
          </dd>
        </div>
      </dl>

      <footer class="mt-auto pt-2">
        <UButton
          :to="wrappedMonthLocation(month)"
          color="neutral"
          variant="outline"
          class="w-full justify-center"
        >
          Open {{ label }} recap
        </UButton>
      </footer>
    </template>
  </article>
</template>

<style scoped>
.monthly-bar {
  background: var(--wrapped-blue);
}

.monthly-bar--busiest {
  background: var(--wrapped-teal);
}

.monthly-quiet {
  background: var(--wrapped-line);
}

.monthly-future {
  border-color: var(--wrapped-line);
}
</style>
