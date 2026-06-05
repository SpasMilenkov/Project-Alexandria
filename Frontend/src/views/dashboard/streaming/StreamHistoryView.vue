<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { StreamHistoryQuery } from "@/api/streaming";

import { getHistory } from "@/queries/streaming";
import { formatDate, formatDuration } from "@/utils/date-formatters";

const currentPage = ref(1);
const pageSize = ref(20);
const completedFilter = ref<boolean | undefined>(undefined);
const expandedRows = ref<Set<string>>(new Set());

const query = computed<StreamHistoryQuery>(() => ({
  currentPage: currentPage.value,
  pageSize: pageSize.value,
  completed: completedFilter.value,
}));

const { data, isLoading, error } = useQuery(() => getHistory(query.value));

const toggleRow = (id: string) => {
  if (expandedRows.value.has(id)) {
    expandedRows.value.delete(id);
  } else {
    expandedRows.value.add(id);
  }
};

</script>

<template>
  <div class="px-4 sm:px-6 py-6 sm:py-8">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6">
      <div>
        <h1 class="text-lg font-semibold text-gray-800 dark:text-white/90 m-0">Stream History</h1>
        <p class="text-xs text-gray-400 dark:text-white/30 mt-0.5 m-0">
          {{ data?.totalCount ?? 0 }} entries
        </p>
      </div>

      <!-- Filter pills -->
      <div class="flex items-center gap-1.5 self-start sm:self-auto">
        <button
          v-for="opt in [
            { label: 'All', value: undefined },
            { label: 'In Progress', value: false },
            { label: 'Completed', value: true },
          ]"
          :key="String(opt.value)"
          class="px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
          :class="
            completedFilter === opt.value
              ? 'bg-primary text-white'
              : 'bg-black/[0.04] dark:bg-white/[0.06] text-gray-600 dark:text-white/50 hover:bg-black/[0.07] dark:hover:bg-white/[0.09]'
          "
          @click="
            completedFilter = opt.value;
            currentPage = 1;
          "
        >
          {{ opt.label }}
        </button>
      </div>
    </div>

    <!-- Skeleton -->
    <div v-if="isLoading" class="space-y-2">
      <div
        v-for="i in pageSize"
        :key="i"
        class="h-14 rounded-xl bg-gray-100/60 dark:bg-white/[0.03] animate-pulse"
      />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load history</p>
    </div>

    <!-- Empty -->
    <div
      v-else-if="!data?.items?.length"
      class="flex flex-col items-center gap-3 py-20 text-center"
    >
      <Icon icon="mdi:history" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">No history yet</p>
    </div>

    <!-- List -->
    <div
      v-else
      class="rounded-xl border border-black/[0.06] dark:border-white/[0.07] overflow-hidden divide-y divide-black/[0.05] dark:divide-white/[0.06]"
    >
      <!-- Desktop table header — hidden on mobile -->
      <div
        class="hidden sm:grid sm:grid-cols-[1fr_120px_110px_32px] items-center px-4 py-2.5 bg-gray-50/80 dark:bg-white/[0.02]"
      >
        <span class="text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
          >File</span
        >
        <span class="text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
          >Status</span
        >
        <span class="text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
          >Last Accessed</span
        >
        <span />
      </div>

      <template v-for="entry in data.items" :key="entry.id">
        <!-- Row trigger -->
        <button
          type="button"
          class="w-full text-left transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.03] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          @click="toggleRow(entry.id)"
        >
          <!-- Mobile layout -->
          <div class="flex items-center gap-3 px-4 py-3 sm:hidden">
            <!-- Icon -->
            <div
              class="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center"
              :class="
                entry.timesCompleted > 0
                  ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-500'
                  : 'bg-amber-50 dark:bg-amber-500/10 text-amber-500'
              "
            >
              <Icon
                :icon="
                  entry.timesCompleted > 0 ? 'mdi:check-circle-outline' : 'mdi:play-circle-outline'
                "
                class="w-4 h-4"
              />
            </div>

            <div class="flex-1 min-w-0">
              <p
                class="text-sm font-medium text-gray-800 dark:text-white/85 truncate m-0 leading-snug"
              >
                {{ entry.title }}
              </p>
              <p class="text-xs text-gray-400 dark:text-white/30 m-0 mt-0.5 leading-snug">
                {{ formatDate(entry.lastAccessedAt) }}
              </p>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <span
                class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-medium"
                :class="
                  entry.timesCompleted > 0
                    ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                    : 'bg-amber-50 dark:bg-amber-500/10 text-amber-600 dark:text-amber-400'
                "
              >
                <template v-if="entry.timesCompleted > 1">{{ entry.timesCompleted }}×</template>
                <template v-else-if="entry.timesCompleted === 1">Done</template>
                <template v-else>Active</template>
              </span>
              <Icon
                icon="mdi:chevron-down"
                class="w-4 h-4 text-gray-400 dark:text-white/25 transition-transform duration-200"
                :class="{ 'rotate-180': expandedRows.has(entry.id) }"
              />
            </div>
          </div>

          <!-- Desktop layout -->
          <div class="hidden sm:grid sm:grid-cols-[1fr_120px_110px_32px] items-center px-4 py-3">
            <!-- File name -->
            <div class="min-w-0 pr-4">
              <p class="text-sm font-medium text-gray-800 dark:text-white/80 truncate m-0">
                {{ entry.title }}
              </p>
            </div>

            <!-- Status -->
            <div>
              <span
                class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-medium"
                :class="
                  entry.timesCompleted > 0
                    ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                    : 'bg-amber-50 dark:bg-amber-500/10 text-amber-600 dark:text-amber-400'
                "
              >
                <Icon
                  :icon="
                    entry.timesCompleted > 0
                      ? 'mdi:check-circle-outline'
                      : 'mdi:play-circle-outline'
                  "
                  class="w-3.5 h-3.5"
                />
                <template v-if="entry.timesCompleted > 1"
                  >{{ entry.timesCompleted }}× Done</template
                >
                <template v-else-if="entry.timesCompleted === 1">Completed</template>
                <template v-else>In Progress</template>
              </span>
            </div>

            <!-- Last accessed -->
            <p class="text-xs text-gray-400 dark:text-white/30 tabular-nums m-0">
              {{ formatDate(entry.lastAccessedAt) }}
            </p>

            <!-- Chevron -->
            <div class="flex justify-end">
              <Icon
                icon="mdi:chevron-down"
                class="w-4 h-4 text-gray-400 dark:text-white/25 transition-transform duration-200"
                :class="{ 'rotate-180': expandedRows.has(entry.id) }"
              />
            </div>
          </div>
        </button>

        <!-- Expanded detail panel -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out overflow-hidden"
          leave-active-class="transition-all duration-150 ease-in overflow-hidden"
          enter-from-class="opacity-0 max-h-0"
          enter-to-class="opacity-100 max-h-64"
          leave-from-class="opacity-100 max-h-64"
          leave-to-class="opacity-0 max-h-0"
        >
          <div
            v-if="expandedRows.has(entry.id)"
            class="bg-gray-50/60 dark:bg-white/[0.02] border-t border-black/[0.04] dark:border-white/[0.05] px-4 py-3"
          >
            <dl class="grid grid-cols-2 sm:grid-cols-4 gap-x-6 gap-y-3">
              <div>
                <dt class="text-xs text-gray-400 dark:text-white/30 mb-0.5">Resume At</dt>
                <dd class="text-sm font-medium tabular-nums text-gray-700 dark:text-white/70 m-0">
                  {{ formatDuration(entry.positionSeconds) }}
                  <span class="text-xs font-normal text-gray-400 dark:text-white/25 ml-1">
                    / {{ formatDuration(entry.maxPositionReachedSeconds) }} max
                  </span>
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-400 dark:text-white/30 mb-0.5">Total Listened</dt>
                <dd class="text-sm font-medium tabular-nums text-gray-700 dark:text-white/70 m-0">
                  {{ formatDuration(entry.totalListenedSeconds) }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-400 dark:text-white/30 mb-0.5">Times Completed</dt>
                <dd class="text-sm font-medium tabular-nums text-gray-700 dark:text-white/70 m-0">
                  {{ entry.timesCompleted }}
                </dd>
              </div>

              <div>
                <dt class="text-xs text-gray-400 dark:text-white/30 mb-0.5">Last Completed</dt>
                <dd class="text-sm font-medium text-gray-700 dark:text-white/70 m-0">
                  <span v-if="entry.lastCompletedAt">{{
                    formatDate(entry.lastCompletedAt)
                  }}</span>
                  <span v-else class="text-gray-300 dark:text-white/20">—</span>
                </dd>
              </div>
            </dl>
          </div>
        </Transition>
      </template>
    </div>

    <!-- Pagination -->
    <div
      v-if="data && (data.totalPages ?? 1) > 1"
      class="flex items-center justify-center gap-2 mt-5"
    >
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.04] dark:bg-white/[0.06] hover:bg-black/[0.07] dark:hover:bg-white/[0.09] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="currentPage <= 1"
        @click="currentPage--"
      >
        <Icon icon="mdi:chevron-left" class="w-4 h-4" /> Prev
      </button>
      <span class="text-sm text-gray-400 dark:text-white/35 tabular-nums px-1">
        {{ currentPage }} / {{ data.totalPages }}
      </span>
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.04] dark:bg-white/[0.06] hover:bg-black/[0.07] dark:hover:bg-white/[0.09] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="currentPage >= (data.totalPages ?? 1)"
        @click="currentPage++"
      >
        Next <Icon icon="mdi:chevron-right" class="w-4 h-4" />
      </button>
    </div>
  </div>
</template>
