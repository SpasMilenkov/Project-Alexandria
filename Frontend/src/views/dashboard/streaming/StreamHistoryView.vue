<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { ref, computed } from "vue";

import type { StreamHistoryQuery } from "@/api/streaming";

import { getHistory } from "@/queries/streaming";

const currentPage = ref(1);
const pageSize = ref(20);
const completedFilter = ref<boolean | undefined>(undefined);

const query = computed<StreamHistoryQuery>(() => ({
  currentPage: currentPage.value,
  pageSize: pageSize.value,
  completed: completedFilter.value,
}));

const { data, isLoading, error } = useQuery(() => getHistory(query.value));

const formatDuration = (seconds: number) => {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return h > 0
    ? `${h}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`
    : `${m}:${String(s).padStart(2, "0")}`;
};

const formatDate = (iso: string) =>
  new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(iso),
  );
</script>

<template>
  <div class="px-6 py-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-lg font-semibold text-gray-800 dark:text-white/90 m-0">Stream History</h1>
        <p class="text-xs text-gray-400 dark:text-white/30 mt-0.5 m-0">
          {{ data?.totalCount ?? 0 }} entries
        </p>
      </div>

      <div class="flex items-center gap-2">
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

    <div v-if="isLoading" class="space-y-2">
      <div
        v-for="i in pageSize"
        :key="i"
        class="h-14 rounded-xl bg-gray-100/60 dark:bg-white/[0.03] animate-pulse"
      />
    </div>

    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load history</p>
    </div>

    <div
      v-else-if="!data?.items?.length"
      class="flex flex-col items-center gap-3 py-20 text-center"
    >
      <Icon icon="mdi:history" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">No history yet</p>
    </div>

    <div
      v-else
      class="rounded-xl border border-black/[0.06] dark:border-white/[0.07] overflow-hidden"
    >
      <table class="w-full text-sm">
        <thead>
          <tr
            class="border-b border-black/[0.06] dark:border-white/[0.07] bg-gray-50/80 dark:bg-white/[0.02]"
          >
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              File
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Resume
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Listened
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Status
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Last Accessed
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-black/[0.04] dark:divide-white/[0.05]">
          <tr
            v-for="entry in data.items"
            :key="entry.id"
            class="hover:bg-black/[0.02] dark:hover:bg-white/[0.03] transition-colors"
          >
            <td
              class="px-4 py-3 font-mono text-xs text-gray-500 dark:text-white/40 truncate max-w-[160px]"
            >
              {{ entry.fileId }}
            </td>

            <td class="px-4 py-3 tabular-nums text-gray-700 dark:text-white/70">
              <span :title="`Max reached: ${formatDuration(entry.maxPositionReachedSeconds)}`">
                {{ formatDuration(entry.positionSeconds) }}
              </span>
            </td>

            <td class="px-4 py-3 tabular-nums text-gray-600 dark:text-white/55">
              {{ formatDuration(entry.totalListenedSeconds) }}
            </td>

            <td class="px-4 py-3">
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
                <template v-if="entry.timesCompleted > 1">
                  Completed {{ entry.timesCompleted }}×
                </template>
                <template v-else-if="entry.timesCompleted === 1"> Completed </template>
                <template v-else> In Progress </template>
              </span>
            </td>

            <td class="px-4 py-3 text-xs text-gray-400 dark:text-white/30">
              {{ formatDate(entry.lastAccessedAt) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div
      v-if="data && (data.totalPages ?? 1) > 1"
      class="flex items-center justify-center gap-2 mt-6"
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
