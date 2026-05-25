<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { ref, computed } from "vue";

import type { TranspilationJobQuery } from "@/api/streaming";

import { TranspilationStatus } from "@/enums/transpilation-status";
import { getTranspilationJobs } from "@/queries/streaming";

const currentPage = ref(1);
const pageSize = ref(20);
const statusFilter = ref<TranspilationStatus | undefined>(undefined);
const isVideoFilter = ref<boolean | undefined>(undefined);

const query = computed<TranspilationJobQuery>(() => ({
  currentPage: currentPage.value,
  pageSize: pageSize.value,
  status: statusFilter.value,
  isVideo: isVideoFilter.value,
}));

const { data, isLoading, error } = useQuery(() => getTranspilationJobs(query.value));

const statusConfig: Record<TranspilationStatus, { label: string; icon: string; class: string }> = {
  [TranspilationStatus.Queued]: {
    label: "Queued",
    icon: "mdi:clock-outline",
    class: "bg-gray-100 dark:bg-white/[0.06] text-gray-500 dark:text-white/40",
  },
  [TranspilationStatus.Partial]: {
    label: "Partial",
    icon: "mdi:clock-outline",
    class: "bg-gray-100 dark:bg-white/[0.06] text-gray-500 dark:text-white/40",
  },
  [TranspilationStatus.Processing]: {
    label: "Processing",
    icon: "mdi:loading",
    class: "bg-blue-50 dark:bg-blue-500/10 text-blue-600 dark:text-blue-400",
  },
  [TranspilationStatus.Ready]: {
    label: "Completed",
    icon: "mdi:check-circle-outline",
    class: "bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  },
  [TranspilationStatus.Failed]: {
    label: "Failed",
    icon: "mdi:alert-circle-outline",
    class: "bg-red-50 dark:bg-red-500/10 text-red-500 dark:text-red-400",
  },
};

const formatDate = (iso?: string) =>
  iso
    ? new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
        new Date(iso),
      )
    : "—";
</script>

<template>
  <div class="px-6 py-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-lg font-semibold text-gray-800 dark:text-white/90 m-0">
          Transpilation Jobs
        </h1>
        <p class="text-xs text-gray-400 dark:text-white/30 mt-0.5 m-0">
          {{ data?.totalCount ?? 0 }} jobs
        </p>
      </div>

      <!-- Filters -->
      <div class="flex items-center gap-2 flex-wrap justify-end">
        <!-- Status filter -->
        <div class="flex items-center gap-1">
          <button
            v-for="opt in [
              { label: 'All', value: undefined },
              { label: 'Queued', value: TranspilationStatus.Queued },
              { label: 'Partial', value: TranspilationStatus.Partial },
              { label: 'Processing', value: TranspilationStatus.Processing },
              { label: 'Completed', value: TranspilationStatus.Ready },
              { label: 'Failed', value: TranspilationStatus.Failed },
            ]"
            :key="String(opt.value)"
            class="px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
            :class="
              statusFilter === opt.value
                ? 'bg-primary text-white'
                : 'bg-black/[0.04] dark:bg-white/[0.06] text-gray-600 dark:text-white/50 hover:bg-black/[0.07] dark:hover:bg-white/[0.09]'
            "
            @click="
              statusFilter = opt.value;
              currentPage = 1;
            "
          >
            {{ opt.label }}
          </button>
        </div>

        <!-- Video/Audio toggle -->
        <div
          class="flex items-center gap-1 border-l border-black/[0.06] dark:border-white/[0.07] pl-2"
        >
          <button
            v-for="opt in [
              { label: 'All', value: undefined, icon: 'mdi:all-inclusive' },
              { label: 'Video', value: true, icon: 'mdi:file-video' },
              { label: 'Audio', value: false, icon: 'mdi:music-note' },
            ]"
            :key="String(opt.value)"
            class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
            :class="
              isVideoFilter === opt.value
                ? 'bg-primary text-white'
                : 'bg-black/[0.04] dark:bg-white/[0.06] text-gray-600 dark:text-white/50 hover:bg-black/[0.07] dark:hover:bg-white/[0.09]'
            "
            @click="
              isVideoFilter = opt.value;
              currentPage = 1;
            "
          >
            <Icon :icon="opt.icon" class="w-3.5 h-3.5" />
            {{ opt.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- Skeleton -->
    <div v-if="isLoading" class="space-y-2">
      <div
        v-for="i in pageSize"
        :key="i"
        class="h-16 rounded-xl bg-gray-100/60 dark:bg-white/[0.03] animate-pulse"
      />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load jobs</p>
    </div>

    <!-- Empty -->
    <div
      v-else-if="!data?.items?.length"
      class="flex flex-col items-center gap-3 py-20 text-center"
    >
      <Icon icon="mdi:cog-outline" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">No jobs found</p>
    </div>

    <!-- Table -->
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
              Version ID
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Type
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Status
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Progress
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Retries
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Created
            </th>
            <th
              class="text-left px-4 py-3 text-xs font-semibold tracking-wide text-gray-400 dark:text-white/30 uppercase"
            >
              Completed
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-black/[0.04] dark:divide-white/[0.05]">
          <tr
            v-for="job in data.items"
            :key="job.id"
            class="hover:bg-black/[0.02] dark:hover:bg-white/[0.03] transition-colors"
          >
            <td
              class="px-4 py-3 font-mono text-xs text-gray-500 dark:text-white/40 truncate max-w-[160px]"
            >
              {{ job.versionId }}
            </td>
            <td class="px-4 py-3">
              <span
                class="inline-flex items-center gap-1.5 text-xs text-gray-600 dark:text-white/50"
              >
                <Icon :icon="job.isVideo ? 'mdi:file-video' : 'mdi:music-note'" class="w-3.5 h-3.5" />
                {{ job.isVideo ? "Video" : "Audio" }}
              </span>
            </td>
            <td class="px-4 py-3">
              <span
                class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-medium"
                :class="statusConfig[job.status].class"
              >
                <Icon
                  :icon="statusConfig[job.status].icon"
                  class="w-3.5 h-3.5"
                  :class="{ 'animate-spin': job.status === TranspilationStatus.Processing }"
                />
                {{ statusConfig[job.status].label }}
              </span>
            </td>
            <td class="px-4 py-3 min-w-[120px]">
              <div class="flex items-center gap-2">
                <div
                  class="flex-1 h-1.5 rounded-full bg-gray-100 dark:bg-white/[0.08] overflow-hidden"
                >
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    :class="
                      job.status === TranspilationStatus.Failed
                        ? 'bg-red-400'
                        : job.status === TranspilationStatus.Ready
                          ? 'bg-emerald-400'
                          : 'bg-primary'
                    "
                    :style="{ width: `${job.progressPercent}%` }"
                  />
                </div>
                <span class="text-xs tabular-nums text-gray-400 dark:text-white/30 w-8 text-right">
                  {{ job.progressPercent }}%
                </span>
              </div>
              <!-- Error detail -->
              <p
                v-if="job.errorDetail"
                class="text-[0.6875rem] text-red-400 mt-0.5 m-0 truncate max-w-[180px]"
              >
                {{ job.errorDetail }}
              </p>
            </td>
            <td class="px-4 py-3 tabular-nums text-xs text-gray-500 dark:text-white/40">
              {{ job.retryCount }}
            </td>
            <td class="px-4 py-3 text-xs text-gray-400 dark:text-white/30">
              {{ formatDate(job.createdAt) }}
            </td>
            <td class="px-4 py-3 text-xs text-gray-400 dark:text-white/30">
              {{ formatDate(job.completedAt) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
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
