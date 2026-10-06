<template>
  <section aria-label="Your recaps" :style="paletteStyle">
    <p class="mb-6 text-sm text-gray-600 dark:text-gray-400">
      Past years freeze into final records. The current year grows as you listen.
    </p>

    <AnnualRecapCard :year="currentYear" current />

    <section class="mt-6" aria-label="Past years">
      <header class="mb-4 flex items-baseline justify-between gap-4">
        <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Past years</h2>

        <p
          v-if="!isPending && !visibleError && !authWaiting"
          class="text-xs text-gray-500 tabular-nums dark:text-gray-500"
        >
          {{ rows.length }} {{ rows.length === 1 ? "recap" : "recaps" }}
        </p>
      </header>

      <div
        v-if="isPending || authWaiting"
        class="flex justify-center py-16"
        role="status"
        aria-label="Loading past recaps"
      >
        <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-400 dark:text-gray-600" />
      </div>
      <div v-else-if="visibleError" class="frosted-glass glass-surface rounded-3xl p-8 text-center">
        <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load past recaps</p>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Try again to see your archived years.
        </p>

        <UButton class="mt-4" variant="outline" color="neutral" @click="refetch()"
          >Try again</UButton
        >
      </div>
      <div
        v-else-if="!rows.length"
        class="frosted-glass glass-surface flex flex-col items-center rounded-3xl border border-gray-200/70 px-4 py-8 text-center dark:border-gray-700/70"
      >
        <UIcon name="mdi:gift-outline" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <h3 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">No archived years yet</h3>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Finalized annual recaps will appear here. This year's listening is above.
        </p>
      </div>
      <div v-else class="grid grid-cols-1 gap-4 md:grid-cols-2">
        <AnnualRecapCard
          v-for="row in rows"
          :key="row.id"
          :year="summaryYear(row.periodStart)"
          :header="row"
        />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { isAxiosError } from "axios";
import { computed } from "vue";

import AnnualRecapCard from "@/components/streaming/wrapped/AnnualRecapCard.vue";
import { useWrappedPalette } from "@/composables/useWrappedPalette";
import { listSummaries } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { annualRecapHeaders } from "@/utils/listening-recaps.utils";
import { SUMMARY_KIND_WRAPPED, summaryYear } from "@/utils/wrapped-summary.utils";

const auth = useAuthStore();
const { paletteStyle } = useWrappedPalette();

const userId = computed(() => auth.user?.user.id);

const currentYear = new Date().getUTCFullYear();

const { data, status, error, refetch } = useQuery(() =>
  listSummaries({ kind: SUMMARY_KIND_WRAPPED, userId: userId.value }),
);

const isPending = computed(() => status.value === "pending");

const isAuthError = (err: unknown): boolean => {
  if (isAxiosError(err)) return err.response?.status === 401 || err.response?.status === 403;
  if (!err || typeof err !== "object") return false;

  const record = err as Record<string, unknown>;

  return record.status === 401 || record.status === 403;
};

const authWaiting = computed(() => !userId.value || isAuthError(error.value));

const visibleError = computed(() => error.value && !isAuthError(error.value));

const rows = computed(() => annualRecapHeaders(data.value ?? [], currentYear));
</script>
