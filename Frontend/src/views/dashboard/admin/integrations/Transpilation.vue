<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <IntegrationPageHeader
      title="Transpilation"
      icon="mdi:file-swap-outline"
      :services="[ServiceType.Transpilation]"
    >
      <template #actions>
        <SegmentedControl v-model="rangeMs" :options="rangeOptions" label="Time range" />
        <SegmentedControl v-model="bucket" :options="bucketOptions" label="Bucket size" />
      </template>
    </IntegrationPageHeader>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="flex flex-col gap-3">
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatTile label="Queue depth" :loading="overviewLoading">{{ queueDepth }}</StatTile>
          <StatTile
            label="Failed jobs"
            :loading="overviewLoading"
            :tone="failedAllTime > 0 ? 'danger' : 'default'"
          >
            {{ failedAllTime }}
          </StatTile>
          <StatTile label="Success rate (window)" :loading="trendLoading">
            {{ windowSuccessRate === null ? "—" : `${windowSuccessRate}%` }}
          </StatTile>
          <StatTile label="Avg duration (30d)" :loading="overviewLoading">
            {{ overview?.duration ? `${Math.round(overview.duration.avgMinutes)} min` : "—" }}
          </StatTile>
        </div>

        <div class="grid grid-cols-1 gap-3 xl:grid-cols-2">
          <IntegrationPanel title="Failure rate">
            <ChartFrame :loading="trendLoading">
              <Line :data="rateChartData" :options="rateOptions" />
            </ChartFrame>
          </IntegrationPanel>

          <IntegrationPanel title="Jobs created">
            <ChartFrame :loading="trendLoading">
              <Bar :data="volumeChartData" :options="baseOptions" />
            </ChartFrame>
          </IntegrationPanel>
        </div>

        <JobRecoveryPanel :type="JobType.Transpilation" title="Transpilation jobs" />

        <section aria-labelledby="transpilation-incidents" class="flex flex-col gap-2">
          <h2
            id="transpilation-incidents"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            Incidents
          </h2>
          <EventsFeed
            :date-range="feedDateRange"
            :initial-service-type="ServiceType.Transpilation"
            :initial-severity="initialSeverity"
            :highlight-id="highlightId"
            locked-service
          />
        </section>
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
import JobRecoveryPanel from "@/components/dashboard/admin/monitoring/JobRecoveryPanel.vue";
import ChartFrame from "@/components/dashboard/integrations/ChartFrame.vue";
import IntegrationPageHeader from "@/components/dashboard/integrations/IntegrationPageHeader.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import SegmentedControl from "@/components/dashboard/integrations/SegmentedControl.vue";
import StatTile from "@/components/dashboard/integrations/StatTile.vue";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useRollingNow } from "@/composables/useRollingNow";
import { useServiceDeepLink } from "@/composables/useServiceDeepLink";
import { useTheme } from "@/composables/useTheme";
import { JobType, ServiceType } from "@/enums";
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

const { feedDateRange, highlightId, initialSeverity } = useServiceDeepLink();

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

// Minute ticks move the trailing window forward, which refetches the trend under a new key.
const { now } = useRollingNow();

const trendParams = computed(() => {
  const to = now.value;
  return {
    from: new Date(to - rangeMs.value).toISOString(),
    to: new Date(to).toISOString(),
    bucket: bucket.value,
  };
});

const {
  data: overview,
  isPending: overviewLoading,
  refetch: refetchOverview,
} = useQuery(transpilationOverview());

// Overview counts have a fixed key, so they are polled explicitly.
useLiveRefresh(refetchOverview, 60_000);
const { data: trend, isPending: trendLoading } = useQuery(() =>
  transpilationTrend(trendParams.value),
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
