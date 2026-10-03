<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { StreamHistoryQuery } from "@/api/streaming";

import { getHistory } from "@/queries/streaming";
import { formatDate, formatDuration } from "@/utils/date-formatters";

const pageSize = 20;
const currentPage = ref(1);
const completedFilter = ref<boolean | undefined>(undefined);

const query = computed<StreamHistoryQuery>(() => ({
  completed: completedFilter.value,
  currentPage: currentPage.value,
  pageSize,
}));

const { data, error, isLoading, refetch } = useQuery(() => getHistory(query.value));

type HistoryEntry = NonNullable<NonNullable<typeof data.value>["items"]>[number];
type DateInput = Parameters<typeof formatDate>[0];

const filterDefs: { label: string; value: boolean | undefined }[] = [
  { label: "All", value: undefined },
  { label: "In progress", value: false },
  { label: "Completed", value: true },
];

const filterOptions = computed(() =>
  filterDefs.map((def) => {
    const isActive = completedFilter.value === def.value;
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
  completedFilter.value = value;
  currentPage.value = 1;
};

const totalCount = computed(() => data.value?.totalCount ?? 0);
const totalPages = computed(() => data.value?.totalPages ?? 1);
const hasItems = computed(() => (data.value?.items?.length ?? 0) > 0);

const countText = computed(
  () => `${totalCount.value} ${totalCount.value === 1 ? "entry" : "entries"}`,
);

const rangeText = computed(() => {
  const start = (currentPage.value - 1) * pageSize + 1;
  const end = Math.min(currentPage.value * pageSize, totalCount.value);
  return `Showing ${start} to ${end} of ${totalCount.value}`;
});

const emptyState = computed(() => {
  if (completedFilter.value === true) {
    return { canReset: true, title: "Nothing completed yet" };
  }
  if (completedFilter.value === false) {
    return { canReset: true, title: "Nothing in progress" };
  }
  return { canReset: false, title: "No history yet" };
});

const relativeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

// Relative time for the last week, absolute date for anything older
const formatWhen = (value: DateInput) => {
  const diffMinutes = Math.round((new Date(value as string).getTime() - Date.now()) / 60000);
  const absMinutes = Math.abs(diffMinutes);
  if (Number.isNaN(diffMinutes) || absMinutes >= 7 * 24 * 60) {
    return formatDate(value);
  }
  if (absMinutes < 1) {
    return "Just now";
  }
  if (absMinutes < 60) {
    return relativeFormatter.format(diffMinutes, "minute");
  }
  if (absMinutes < 24 * 60) {
    return relativeFormatter.format(Math.round(diffMinutes / 60), "hour");
  }
  return relativeFormatter.format(Math.round(diffMinutes / 1440), "day");
};

const statusOf = (entry: HistoryEntry) => {
  if (entry.timesCompleted > 1) {
    return {
      color: "success" as const,
      icon: "mdi:check-circle-outline",
      label: `Completed ${entry.timesCompleted}×`,
    };
  }
  if (entry.timesCompleted === 1) {
    return { color: "success" as const, icon: "mdi:check-circle-outline", label: "Completed" };
  }
  return { color: "warning" as const, icon: "mdi:play-circle-outline", label: "In progress" };
};

const subtitleOf = (entry: HistoryEntry) => {
  if (entry.timesCompleted > 0) {
    return `Listened ${formatDuration(entry.totalListenedSeconds)}`;
  }
  return `Resume at ${formatDuration(entry.positionSeconds)}`;
};
</script>

<template>
  <div class="px-4 sm:px-6 py-5">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
      <div>
        <h1 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Stream history</h1>
        <p class="text-sm text-gray-600 dark:text-gray-400">{{ countText }}</p>
      </div>

      <div
        class="inline-flex self-start sm:self-auto gap-1 p-0.5 rounded-lg bg-gray-100 dark:bg-gray-800"
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

    <div v-else-if="error" class="flex flex-col items-center gap-2 py-16 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
        Couldn't load history
      </h2>
      <p class="text-sm text-gray-600 dark:text-gray-400">
        Something went wrong while loading your history. Try again in a moment.
      </p>
      <UButton class="mt-2" color="neutral" variant="outline" size="sm" @click="refetch()">
        Try again
      </UButton>
    </div>

    <div v-else-if="!hasItems" class="flex flex-col items-center gap-2 py-16 text-center">
      <Icon icon="mdi:history" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
        {{ emptyState.title }}
      </h2>
      <p class="text-sm text-gray-600 dark:text-gray-400">
        Things you play will show up here so you can pick up where you left off.
      </p>
      <UButton
        v-if="emptyState.canReset"
        class="mt-2"
        color="neutral"
        variant="outline"
        size="sm"
        @click="setFilter(undefined)"
      >
        Show all
      </UButton>
    </div>

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
              class="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400"
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
                :title="String(formatDate(entry.lastAccessedAt))"
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
                  {{ formatDuration(entry.positionSeconds) }}
                </dd>
                <dd class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">
                  Furthest {{ formatDuration(entry.maxPositionReachedSeconds) }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Total listened</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100 tabular-nums">
                  {{ formatDuration(entry.totalListenedSeconds) }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Times completed</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100 tabular-nums">
                  {{ entry.timesCompleted }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-600 dark:text-gray-400">Last completed</dt>
                <dd class="text-sm font-medium text-gray-900 dark:text-gray-100">
                  <span v-if="entry.lastCompletedAt">{{ formatWhen(entry.lastCompletedAt) }}</span>
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
