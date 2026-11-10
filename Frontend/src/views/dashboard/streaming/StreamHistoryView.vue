<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { StreamHistoryQuery } from "@/api/streaming";

import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import StreamHistoryGhost from "@/components/streaming/ghosts/StreamHistoryGhost.vue";
import { getHistory } from "@/queries/streaming";
import { formatCount } from "@/utils/count";
import {
  type DateInput,
  formatCalendarDate,
  formatPlaybackTime,
  formatTimeAgo,
} from "@/utils/date-formatters";

const pageSize = 20;
const currentPage = ref(1);
const qualifiedFilter = ref<boolean | undefined>(undefined);

const query = computed<StreamHistoryQuery>(() => ({
  qualified: qualifiedFilter.value,
  currentPage: currentPage.value,
  pageSize,
}));

const { data, error, isLoading, refetch } = useQuery(() => getHistory(query.value));

type HistoryEntry = NonNullable<NonNullable<typeof data.value>["items"]>[number];

const filterDefs: { label: string; value: boolean | undefined }[] = [
  { label: "All", value: undefined },
  { label: "Brief listens", value: false },
  { label: "Played", value: true },
];

const filterOptions = computed(() =>
  filterDefs.map((def) => {
    const isActive = qualifiedFilter.value === def.value;
    return {
      ...def,
      color: (isActive ? "primary" : "neutral") as "neutral" | "primary",
      isActive,
      key: String(def.value),
      variant: (isActive ? "subtle" : "ghost") as "ghost" | "subtle",
    };
  }),
);

const setFilter = (value: boolean | undefined) => {
  qualifiedFilter.value = value;
  currentPage.value = 1;
};

const totalCount = computed(() => data.value?.totalCount ?? 0);
const totalPages = computed(() => data.value?.totalPages ?? 1);
const hasItems = computed(() => (data.value?.items?.length ?? 0) > 0);

const countText = computed(() => formatCount(totalCount.value, "entry", "entries"));

const rangeText = computed(() => {
  const start = (currentPage.value - 1) * pageSize + 1;
  const end = Math.min(currentPage.value * pageSize, totalCount.value);
  return `Showing ${start} to ${end} of ${totalCount.value}`;
});

const emptyState = computed(() => {
  if (qualifiedFilter.value === true) {
    return { canReset: true, title: "Nothing played yet" };
  }
  if (qualifiedFilter.value === false) {
    return { canReset: true, title: "No brief listens" };
  }
  return { canReset: false, title: "No history yet" };
});

// Relative time for the last week, absolute date for anything older
const formatWhen = (value: DateInput) => formatTimeAgo(value, { absoluteAfterDays: 7 });

const statusOf = (entry: HistoryEntry) => {
  if (entry.qualifiedPlayCount > 1) {
    return {
      color: "success" as const,
      icon: "mdi:check-circle-outline",
      label: `Played ${entry.qualifiedPlayCount} times`,
    };
  }
  if (entry.qualifiedPlayCount === 1) {
    return { color: "success" as const, icon: "mdi:check-circle-outline", label: "Played" };
  }
  return { color: "warning" as const, icon: "mdi:play-circle-outline", label: "Brief listens" };
};

const subtitleOf = (entry: HistoryEntry) => {
  if (entry.qualifiedPlayCount > 0) {
    return `Listened ${formatPlaybackTime(entry.totalListenedSeconds)}`;
  }
  return `Resume at ${formatPlaybackTime(entry.positionSeconds)}`;
};
</script>

<template>
  <div class="flex min-h-full flex-col px-4 py-5 sm:px-6">
    <div
      class="rounded-xl p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6 frosted-glass glass-surface"
    >
      <div>
        <h1 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Stream history</h1>
        <p class="text-sm text-gray-600 dark:text-gray-400">{{ countText }}</p>
      </div>

      <div
        class="inline-flex self-start sm:self-auto gap-1 p-0.5 rounded-lg bg-neutral-100 dark:bg-neutral-800"
        role="group"
        aria-label="Filter by status"
      >
        <UButton
          v-for="option in filterOptions"
          :key="option.key"
          size="xs"
          :color="option.color"
          :variant="option.variant"
          :aria-pressed="option.isActive"
          @click="setFilter(option.value)"
        >
          {{ option.label }}
        </UButton>
      </div>
    </div>

    <div
      v-if="isLoading"
      class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden divide-y divide-gray-100/50 dark:divide-gray-800/50"
    >
      <div v-for="n in 6" :key="n" class="flex items-center gap-4 px-4 py-3">
        <USkeleton class="w-8 h-8 rounded-lg shrink-0" />
        <USkeleton class="h-4 flex-1" />
        <USkeleton class="h-5 w-24" />
      </div>
    </div>

    <div v-else-if="error" class="flex py-16">
      <GlassEmptyState
        icon="i-mdi-alert-circle-outline"
        title="Couldn't load history"
        description="Something went wrong while loading your history. Try again in a moment."
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="refetch()">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <EmptyStatePanel v-else-if="!hasItems">
      <template #ghost>
        <StreamHistoryGhost />
      </template>

      <GlassEmptyState
        icon="i-mdi-history"
        :title="emptyState.title"
        description="Things you play will show up here so you can pick up where you left off."
        class="m-auto"
      >
        <UButton
          v-if="emptyState.canReset"
          color="neutral"
          variant="outline"
          class="rounded-full px-5"
          @click="setFilter(undefined)"
        >
          Show all
        </UButton>

        <UButton v-else to="/media" color="neutral" variant="outline" class="rounded-full px-5">
          Browse media
        </UButton>
      </GlassEmptyState>
    </EmptyStatePanel>

    <template v-else>
      <div
        class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden divide-y divide-gray-100/50 dark:divide-gray-800/50"
      >
        <UCollapsible v-for="entry in data?.items" :key="entry.id">
          <button
            type="button"
            class="group w-full flex items-center gap-4 px-4 py-3 text-left transition-colors hover:bg-gray-50 dark:hover:bg-white/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary"
          >
            <div
              class="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center bg-neutral-100 dark:bg-neutral-800 text-gray-600 dark:text-gray-400"
            >
              <Icon :icon="statusOf(entry).icon" class="w-4 h-4" />
            </div>

            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
                {{ entry.title }}
              </p>
              <p class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">
                {{ subtitleOf(entry) }}
              </p>
            </div>

            <div
              class="shrink-0 flex flex-col items-end gap-1 sm:flex-row sm:items-center sm:gap-4"
            >
              <UBadge :color="statusOf(entry).color" variant="subtle" size="sm">
                {{ statusOf(entry).label }}
              </UBadge>
              <span
                class="text-xs text-gray-500 dark:text-gray-500 tabular-nums sm:w-28 sm:text-right"
                :title="formatCalendarDate(entry.lastAccessedAt)"
              >
                {{ formatWhen(entry.lastAccessedAt) }}
              </span>
            </div>

            <Icon
              icon="mdi:chevron-down"
              class="shrink-0 w-4 h-4 text-gray-500 dark:text-gray-500 transition-transform duration-200 group-data-[state=open]:rotate-180"
            />
          </button>

          <template #content>
            <dl
              class="grid grid-cols-2 sm:grid-cols-4 gap-x-6 gap-y-4 px-4 py-4 bg-gray-50 dark:bg-white/5"
            >
              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Resume at</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100 tabular-nums">
                  {{ formatPlaybackTime(entry.positionSeconds) }}
                </dd>
                <dd class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">
                  Furthest {{ formatPlaybackTime(entry.maxPositionReachedSeconds) }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Total listened</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100 tabular-nums">
                  {{ formatPlaybackTime(entry.totalListenedSeconds) }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Times played</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100 tabular-nums">
                  {{ entry.qualifiedPlayCount }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Last played</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100">
                  <span v-if="entry.lastPlayedAt">{{ formatWhen(entry.lastPlayedAt) }}</span>
                  <span v-else class="text-gray-500 dark:text-gray-500">Never</span>
                </dd>
              </div>
            </dl>
          </template>
        </UCollapsible>
      </div>

      <div
        v-if="totalPages > 1"
        class="flex flex-col sm:flex-row items-center justify-between gap-4 mt-6"
      >
        <p class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">{{ rangeText }}</p>
        <UPagination
          v-model:page="currentPage"
          :total="totalCount"
          :items-per-page="pageSize"
          :sibling-count="1"
          size="sm"
          color="neutral"
          variant="ghost"
          active-color="primary"
          active-variant="subtle"
        />
      </div>
    </template>
  </div>
</template>
