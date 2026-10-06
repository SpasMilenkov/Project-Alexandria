<template>
  <div class="relative isolate overflow-x-clip" :style="paletteStyle">
    <WrappedBackdrop />

    <main class="mx-auto w-full max-w-[1200px] px-4 py-6 md:px-6 md:py-8">
      <header class="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <p
            class="text-xs font-semibold tracking-[0.16em] text-gray-600 uppercase dark:text-gray-400"
          >
            Your year, on record
          </p>

          <h1 class="mt-2 text-3xl font-black tracking-tight text-gray-900 dark:text-gray-100">
            {{ year }} Wrapped
          </h1>

          <div class="mt-2 flex flex-wrap items-center gap-2">
            <p class="text-sm text-gray-600 dark:text-gray-400">{{ periodLabel }}</p>
            <UBadge v-if="isFinal" color="neutral" variant="subtle">Final record</UBadge>
            <UBadge v-else-if="hasDeck" color="neutral" variant="subtle">So far</UBadge>
          </div>
        </div>

        <div class="flex shrink-0 flex-wrap items-center gap-2" role="group" aria-label="Wrapped controls">
          <USelect
            :model-value="year"
            :items="yearOptions"
            class="w-24"
            aria-label="Wrapped year"
            @update:model-value="goToYear"
          />

          <WrappedPaletteSwitcher v-if="hasDeck" v-model="palette" />

          <UDropdownMenu :items="moreActions" :content="{ align: 'end' }">
            <UButton
              icon="i-mdi-dots-horizontal"
              color="neutral"
              variant="ghost"
              aria-label="More Wrapped options"
              :loading="isFetching"
            />
          </UDropdownMenu>

          <div v-if="hasDeck" class="ml-auto md:ml-0">
            <WrappedExportButton
              :key="`${userId}-${year}`"
              :response="deck!"
              :year="year"
              :palette="palette"
            />
          </div>
        </div>
      </header>

      <div
        v-if="isPending || authWaiting"
        class="flex justify-center py-16"
        role="status"
        aria-label="Building your Wrapped"
      >
        <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-400 dark:text-gray-600" />
      </div>
      <div v-else-if="visibleError" class="frosted-glass glass-surface rounded-3xl p-8 text-center">
        <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load your Wrapped</p>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Something went wrong building your recap.
        </p>

        <UButton class="mt-4" variant="outline" color="neutral" @click="refetch()"
          >Try again</UButton
        >
      </div>
      <div
        v-else-if="unsupportedPayload"
        class="frosted-glass glass-surface rounded-3xl p-8 text-center"
      >
        <p class="font-semibold text-gray-900 dark:text-gray-100">This recap uses a newer format</p>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Update the app to view this year's Wrapped.
        </p>
      </div>
      <div
        v-else-if="!deck?.deck.cards.length"
        class="flex flex-col items-center py-16 text-center "
      >
        <UIcon name="mdi:music-note" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <h2 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">{{ emptyState.title }}</h2>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          {{ emptyState.description }}
        </p>

        <UButton v-if="isLatestYear" class="mt-6" to="/streaming/music" color="primary">{{
          emptyState.actionLabel
        }}</UButton>
        <UButton v-else class="mt-6" color="primary" @click="goToYear(currentYear)">{{
          emptyState.actionLabel
        }}</UButton>
      </div>
      <WrappedExperience
        v-else
        :key="`${userId}-${year}`"
        :response="deck!"
        :year="year"
        :palette="palette"
      />
    </main>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { isAxiosError } from "axios";
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import WrappedBackdrop from "@/components/streaming/wrapped/WrappedBackdrop.vue";
import WrappedExperience from "@/components/streaming/wrapped/WrappedExperience.vue";
import WrappedExportButton from "@/components/streaming/wrapped/WrappedExportButton.vue";
import WrappedPaletteSwitcher from "@/components/streaming/wrapped/WrappedPaletteSwitcher.vue";
import { useWrappedPalette } from "@/composables/useWrappedPalette";
import { getSummary } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import {
  MIN_WRAPPED_YEAR,
  parseWrappedYear,
  wrappedEmptyState,
  yearRange,
} from "@/utils/wrapped-display.utils";
import { SUMMARY_KIND_WRAPPED, summaryToWrappedDeck } from "@/utils/wrapped-summary.utils";

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const { palette, paletteStyle } = useWrappedPalette();

const userId = computed(() => auth.user?.user.id);

const currentYear = new Date().getUTCFullYear();

const year = computed(() => parseWrappedYear(route.params.year, currentYear));

const range = computed(() => yearRange(year.value));

const isLatestYear = computed(() => year.value === currentYear);

const yearOptions = Array.from({ length: currentYear - MIN_WRAPPED_YEAR + 1 }, (_, index) => ({
  label: String(currentYear - index),
  value: currentYear - index,
}));

const emptyState = computed(() => wrappedEmptyState(year.value, currentYear));

// Omit the current end boundary so every refresh resolves a fresh server-side "now".
const { data, status, asyncStatus, error, refetch } = useQuery(() =>
  getSummary({
    kind: SUMMARY_KIND_WRAPPED,
    from: range.value.from,
    to: isLatestYear.value ? undefined : range.value.to,
    userId: userId.value,
  }),
);

const deck = computed(() => (data.value ? summaryToWrappedDeck(data.value) : undefined));

const isFinal = computed(() => Boolean(data.value?.isFinal));

const isPending = computed(() => status.value === "pending");

const isFetching = computed(() => asyncStatus.value === "loading");

const isAuthError = (err: unknown): boolean => {
  if (isAxiosError(err)) return err.response?.status === 401 || err.response?.status === 403;
  if (!err || typeof err !== "object") return false;

  const record = err as Record<string, unknown>;

  return record.status === 401 || record.status === 403;
};

const authWaiting = computed(() => !userId.value || isAuthError(error.value));

const visibleError = computed(() => error.value && !isAuthError(error.value));

const unsupportedPayload = computed(
  () => !isPending.value && !authWaiting.value && !visibleError.value && data.value && !deck.value,
);

const hasDeck = computed(
  () =>
    !isPending.value &&
    !authWaiting.value &&
    !visibleError.value &&
    !unsupportedPayload.value &&
    Boolean(deck.value?.deck.cards.length),
);

const moreActions = computed(() => [
  {
    label: "Refresh recap",
    icon: "i-mdi-refresh",
    disabled: isFetching.value || authWaiting.value,

    onSelect: () => {
      void refetch();
    },
  },
]);

const periodLabel = computed(() => {
  if (!isLatestYear.value)
    return isFinal.value
      ? "Your final record for this year"
      : "A chapter from your year in music";

  if (!deck.value?.generatedAt) return "Your soundtrack so far";

  const through = new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(new Date(deck.value.generatedAt));

  return `Your soundtrack through ${through}`;
});

const goToYear = (next: number) => {
  if (next < MIN_WRAPPED_YEAR || next > currentYear) return;

  void router.push({ name: "wrapped", params: { year: String(next) } });
};
</script>
