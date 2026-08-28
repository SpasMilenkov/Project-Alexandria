<template>
  <div class="p-3 sm:p-5 lg:p-6">
    <div class="max-w-400 mx-auto space-y-3 lg:space-y-4">
      <!-- Page header -->
      <div class="flex items-center justify-between gap-3 flex-wrap mb-1">
        <div>
          <h1 class="text-lg font-semibold text-gray-800 dark:text-gray-100">
            Transpilation Dashboard
          </h1>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
            Queue health, failure trends and throughput for the transpilation worker
          </p>
        </div>
      </div>

      <!-- Stat cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Queue depth
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ queueDepth }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Failed jobs
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p
            v-else
            class="text-2xl font-bold tabular-nums"
            :class="
              failedAllTime > 0
                ? 'text-red-600 dark:text-red-400'
                : 'text-gray-900 dark:text-gray-100'
            "
          >
            {{ failedAllTime }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Success rate (window)
          </p>
          <USkeleton v-if="trendLoading" class="h-7 w-16 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ windowSuccessRate === null ? "—" : `${windowSuccessRate}%` }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Avg duration (30d)
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-20 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ overview?.duration ? `${Math.round(overview.duration.avgMinutes)} min` : "—" }}
          </p>
        </div>
      </div>

      <!-- Charts -->
      <div class="space-y-3">
        <!-- Range / bucket toggles -->
        <div class="flex items-center justify-end gap-2 flex-wrap">
          <div class="flex items-center gap-1">
            <UButton
              v-for="option in rangeOptions"
              :key="option.label"
              size="xs"
              :variant="rangeMs === option.value ? 'solid' : 'outline'"
              :color="rangeMs === option.value ? 'primary' : 'neutral'"
              @click="rangeMs = option.value"
            >
              {{ option.label }}
            </UButton>
          </div>
          <div class="flex items-center gap-1">
            <UButton
              v-for="option in bucketOptions"
              :key="option.label"
              size="xs"
              :variant="bucket === option.value ? 'solid' : 'outline'"
              :color="bucket === option.value ? 'primary' : 'neutral'"
              @click="bucket = option.value"
            >
              {{ option.label }}
            </UButton>
          </div>
        </div>

        <div
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4"
        >
          <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Failure rate %</p>
          <!-- Bounded height is mandatory with maintainAspectRatio:false, else
               the canvas feedback-loops into unbounded page growth -->
          <div v-if="trendLoading" class="h-56">
            <USkeleton class="h-full w-full rounded-xl" />
          </div>
          <div v-else class="relative h-56">
            <Line :data="rateChartData" :options="rateOptions" />
          </div>
        </div>

        <div
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4"
        >
          <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Jobs created</p>
          <div v-if="trendLoading" class="h-56">
            <USkeleton class="h-full w-full rounded-xl" />
          </div>
          <div v-else class="relative h-56">
            <Bar :data="volumeChartData" :options="baseOptions" />
          </div>
        </div>
      </div>

      <!-- Incidents -->
      <div class="space-y-2">
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300 px-1">
          Transpilation incidents
        </h2>
        <EventsFeed
          :date-range="null"
          :initial-service-type="ServiceType.Transpilation"
          locked-service
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import {
  BarElement,
  CategoryScale,
  type ChartData,
  Chart as ChartJS,
  type ChartOptions,
  Legend,
  LineElement,
  LinearScale,
  PointElement,
  Title,
  Tooltip,
  Filler,
} from "chart.js";
import { computed, ref } from "vue";
import { Bar, Line } from "vue-chartjs";

import type { StatsBucket } from "@/api/transpilationStats";

import { STATS_BUCKET, TRANSPILATION_STATUS } from "@/api/transpilationStats";
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import { useTheme } from "@/composables/useTheme";
import { ServiceType } from "@/enums";
import { transpilationOverview, transpilationTrend } from "@/queries/transpilationStats";

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  PointElement,
  BarElement,
  CategoryScale,
  LinearScale,
  Filler,
);

const { isDark } = useTheme();

const rangeOptions = [
  { label: "24h", value: 24 * 60 * 60 * 1000 },
  { label: "7d", value: 7 * 24 * 60 * 60 * 1000 },
] as const;

const bucketOptions: { label: string; value: StatsBucket }[] = [
  { label: "Hourly", value: STATS_BUCKET.Hour },
  { label: "Daily", value: STATS_BUCKET.Day },
];

const rangeMs = ref(rangeOptions[0].value);
const bucket = ref<StatsBucket>(STATS_BUCKET.Hour);

const trendParams = computed(() => {
  const to = Date.now();
  return {
    from: new Date(to - rangeMs.value).toISOString(),
    to: new Date(to).toISOString(),
    bucket: bucket.value,
  };
});

const { data: overview, isLoading: overviewLoading } = useQuery(transpilationOverview());
const { data: trend, isLoading: trendLoading } = useQuery(
  transpilationTrend,
  () => trendParams.value,
);

const statusCount = (status: number): number =>
  overview.value?.statusCounts.find((entry) => entry.status === status)?.count ?? 0;

const queueDepth = computed(
  () =>
    statusCount(TRANSPILATION_STATUS.Queued) +
    statusCount(TRANSPILATION_STATUS.Processing) +
    statusCount(TRANSPILATION_STATUS.CancellationRequested),
);

const failedAllTime = computed(() => statusCount(TRANSPILATION_STATUS.Failed));

const windowSuccessRate = computed(() => {
  const points = trend.value?.failureRate ?? [];
  const total = points.reduce((sum, p) => sum + p.total, 0);
  const failed = points.reduce((sum, p) => sum + p.failed, 0);
  if (total === 0) return null;
  return Math.round((1 - failed / total) * 1000) / 10;
});

const gridColor = computed(() =>
  isDark.value ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)",
);
const tickColor = computed(() => (isDark.value ? "#9ca3af" : "#6b7280"));

const bucketLabel = (iso: string): string => {
  const date = new Date(iso);
  if (bucket.value === STATS_BUCKET.Hour) {
    return date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  }
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
};

const rateChartData = computed<ChartData<"line">>(() => ({
  labels: (trend.value?.failureRate ?? []).map((p) => bucketLabel(p.bucketStart)),
  datasets: [
    {
      label: "Failure rate %",
      data: (trend.value?.failureRate ?? []).map((p) => p.failureRate),
      borderColor: "#dc2626",
      backgroundColor: "rgba(220, 38, 38, 0.15)",
      tension: 0.3,
      fill: true,
      pointRadius: 2,
    },
  ],
}));

const volumeChartData = computed<ChartData<"bar">>(() => ({
  labels: (trend.value?.volume ?? []).map((p) => bucketLabel(p.bucketStart)),
  datasets: [
    {
      label: "Jobs created",
      data: (trend.value?.volume ?? []).map((p) => p.count),
      backgroundColor: "#5B7FA6",
      borderRadius: 3,
    },
  ],
}));

const baseOptions = computed<ChartOptions<any>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: { intersect: false, mode: "index" as const },
  },
  scales: {
    x: { grid: { color: gridColor.value }, ticks: { color: tickColor.value, maxRotation: 0 } },
    y: {
      beginAtZero: true,
      grid: { color: gridColor.value },
      ticks: { color: tickColor.value, precision: 0 },
    },
  },
}));

const rateOptions = computed<ChartOptions<"line">>(() => ({
  ...baseOptions.value,
  scales: {
    ...baseOptions.value.scales,
    y: { ...baseOptions.value.scales.y, max: 100 },
  },
}));
</script>
