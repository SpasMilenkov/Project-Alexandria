<template>
  <article
    ref="cardElement"
    class="frosted-glass glass-surface flex min-w-0 flex-col gap-6 rounded-3xl border border-gray-200/70 p-4 dark:border-gray-700/70 md:p-6"
    :aria-label="current ? `${year} so far` : `${year} Wrapped`"
  >
    <header class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <h2
          v-if="current"
          class="text-xl font-black tracking-tight text-gray-900 dark:text-gray-100"
        >
          {{ year }} so far
        </h2>
        <h3 v-else class="flex items-baseline gap-2 text-gray-900 dark:text-gray-100">
          <UIcon
            name="mdi:gift-outline"
            class="h-5 w-5 self-center text-gray-500 dark:text-gray-500"
          />
          <span class="text-2xl font-black tracking-tight tabular-nums">{{ year }}</span>
          <span class="text-xs font-normal text-gray-500 dark:text-gray-500">Wrapped</span>
        </h3>

        <p v-if="current" class="mt-2 text-xs text-gray-500 dark:text-gray-500">
          Your soundtrack from January to now.
        </p>
      </div>

      <UBadge color="neutral" variant="subtle" class="shrink-0">
        <UIcon v-if="isFinal" name="mdi:lock-outline" class="mr-1 h-3.5 w-3.5" />{{
          isFinal ? "Final" : "So far"
        }}
      </UBadge>
    </header>

    <div
      v-if="!shouldLoad || isPending || authWaiting"
      class="flex flex-1 items-center justify-center py-16"
      :class="current ? 'min-h-56' : 'min-h-48'"
      role="status"
      :aria-label="`Loading ${year} recap`"
    >
      <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-400 dark:text-gray-600" />
    </div>
    <div v-else-if="visibleError" class="flex-1 py-8 text-center">
      <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load {{ year }} recap</p>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Try again to see this year's listening.
      </p>

      <UButton class="mt-4" color="neutral" variant="outline" @click="refetch()">Try again</UButton>
    </div>
    <div v-else-if="!deck" class="flex-1 py-8 text-center">
      <p class="font-semibold text-gray-900 dark:text-gray-100">This recap uses a newer format</p>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Update the app to view this recap.
      </p>
    </div>
    <div v-else-if="!hasListening" class="flex flex-1 flex-col items-center py-8 text-center">
      <UIcon name="mdi:music-note-outline" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
      <h3 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">{{ emptyState.title }}</h3>
      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">{{ emptyState.description }}</p>

      <UButton v-if="current" class="mt-6" to="/streaming/music" color="primary"
        >Find your soundtrack</UButton
      >
    </div>
    <template v-else>
      <p v-if="!current" class="-mt-4 text-sm text-gray-600 tabular-nums dark:text-gray-400">
        <strong class="font-semibold text-gray-900 dark:text-gray-100">{{
          formatWrappedDuration(facts!.seconds)
        }}</strong>
        · {{ numberFormat.format(facts!.qualifiedPlayCount) }} plays
      </p>

      <RecapLandscape
        v-if="landscapeSeries"
        :year="year"
        :series="landscapeSeries"
        :compact="!current"
        :through="current ? deck.to : undefined"
      />
      <p v-else class="text-sm text-gray-600 dark:text-gray-400">
        This recap has no monthly breakdown.
      </p>

      <dl
        class="grid gap-4 border-t border-gray-100/50 pt-4 dark:border-gray-800/50"
        :class="current ? 'grid-cols-2 sm:grid-cols-4' : 'grid-cols-3'"
      >
        <div v-for="fact in displayFacts" :key="fact.label" class="min-w-0">
          <dt class="min-h-8 text-xs text-gray-600 dark:text-gray-400">{{ fact.label }}</dt>
          <dd
            class="mt-2 font-black tracking-tight text-gray-900 tabular-nums dark:text-gray-100"
            :class="current ? 'text-2xl' : 'text-base'"
          >
            {{ fact.value }}
          </dd>
        </div>
      </dl>
    </template>

    <footer class="mt-auto flex flex-wrap items-center justify-between gap-4">
      <p v-if="generatedLabel" class="text-xs text-gray-500 dark:text-gray-500">
        {{ generatedLabel }}
      </p>

      <div class="ml-auto flex items-center gap-2">
        <UButton
          v-if="canFinalize"
          size="sm"
          color="neutral"
          variant="outline"
          :loading="finalizing"
          @click="finalize"
          >Finalize</UButton
        >

        <UButton
          :to="`/stats/wrapped/${year}`"
          :color="current && hasListening ? 'primary' : 'neutral'"
          :variant="current && hasListening ? 'solid' : 'ghost'"
          :size="current ? 'md' : 'sm'"
          >{{ current ? `Open ${year} Wrapped` : "Open" }}</UButton
        >
      </div>
    </footer>
  </article>
</template>

<script setup lang="ts">
import { useToast } from "@nuxt/ui/composables/useToast";
import { useQuery } from "@pinia/colada";
import { useIntersectionObserver } from "@vueuse/core";
import { isAxiosError } from "axios";
import { computed, ref, useTemplateRef, watch } from "vue";

import type { OverviewSummaryHeaderDtoResponse } from "@/api/stats";

import RecapLandscape from "@/components/streaming/wrapped/RecapLandscape.vue";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { finalizeSummary } from "@/mutations/stats";
import { getSummary } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { recapTimestamp } from "@/utils/listening-recaps.utils";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { wrappedEmptyState, yearRange } from "@/utils/wrapped-display.utils";
import { SUMMARY_KIND_WRAPPED, summaryToWrappedDeck } from "@/utils/wrapped-summary.utils";

const {
  year,
  current = false,
  header = undefined,
} = defineProps<{
  year: number;
  current?: boolean;
  header?: OverviewSummaryHeaderDtoResponse;
}>();

const auth = useAuthStore();
const toast = useToast();

const userId = computed(() => auth.user?.user.id);

const cardElement = useTemplateRef("cardElement");
const shouldLoad = ref(Boolean(current));

const { isSupported } = useIntersectionObserver(
  cardElement,
  ([entry]) => {
    if (entry?.isIntersecting) shouldLoad.value = true;
  },
  { rootMargin: "200px" },
);

watch(
  isSupported,
  (supported) => {
    if (!supported) shouldLoad.value = true;
  },
  { immediate: true },
);

const range = computed(() => yearRange(year));

const canLoad = computed(() => shouldLoad.value && Boolean(userId.value));

const summaryOptions = computed(() =>
  getSummary({
    kind: SUMMARY_KIND_WRAPPED,
    from: header?.periodStart ?? range.value.from,
    to: current ? undefined : (header?.periodEnd ?? range.value.to),
    userId: userId.value,
  }),
);

const { data, status, error, refetch } = useQuery({
  ...summaryOptions.value,
  key: () => summaryOptions.value.key,
  query: (context) => summaryOptions.value.query(context),
  enabled: canLoad,
});

const deck = computed(() => (data.value ? summaryToWrappedDeck(data.value) : null));

const facts = computed(() => deck.value?.summary);

const hasListening = computed(() => Boolean(facts.value && facts.value.seconds > 0));

const landscapeSeries = computed(
  () =>
    deck.value?.deck.cards.find((card) => card.type === WrappedCardType.ListeningTime)?.facts
      ?.series,
);

const numberFormat = new Intl.NumberFormat();

const displayFacts = computed(() => {
  if (!facts.value) return [];

  const items = [
    { label: "Tracks", value: numberFormat.format(facts.value.tracks) },
    { label: "Artists", value: numberFormat.format(facts.value.artists) },
    { label: "Listening days", value: numberFormat.format(facts.value.activeDays) },
  ];

  if (current)
    items.unshift({
      label: "Time with your music",
      value: formatWrappedDuration(facts.value.seconds),
    });

  return items;
});

const emptyState = computed(() => wrappedEmptyState(year, new Date().getUTCFullYear()));

const isFinal = computed(() => data.value?.isFinal ?? header?.isFinal ?? false);

const isPending = computed(() => status.value === "pending");

const isAuthError = (err: unknown): boolean => {
  if (isAxiosError(err)) return err.response?.status === 401 || err.response?.status === 403;
  if (!err || typeof err !== "object") return false;

  const record = err as Record<string, unknown>;

  return record.status === 401 || record.status === 403;
};

const authWaiting = computed(() => !userId.value || isAuthError(error.value));

const visibleError = computed(() => error.value && !isAuthError(error.value));

const generatedLabel = computed(() => {
  const date = data.value?.generatedAt ?? header?.generatedAt;

  return date ? recapTimestamp(date, Boolean(current)) : "";
});

const canFinalize = computed(
  () =>
    !current && !isFinal.value && Boolean(header) && Date.parse(header!.periodEnd) <= Date.now(),
);

const { mutateAsync: finalizeNow } = finalizeSummary();
const finalizing = ref(false);

const finalize = async () => {
  if (!header || finalizing.value) return;

  finalizing.value = true;

  try {
    await finalizeNow({
      kind: SUMMARY_KIND_WRAPPED,
      from: header.periodStart,
      to: header.periodEnd,
    });

    toast.add({ title: "Recap finalized", color: "success" });
  } catch {
    toast.add({
      title: "Finalize failed",
      description: "Could not finalize this recap.",
      color: "error",
    });
  } finally {
    finalizing.value = false;
  }
};
</script>
