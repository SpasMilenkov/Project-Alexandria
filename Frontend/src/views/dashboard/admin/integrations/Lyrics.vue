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
} from "chart.js";
import { computed, ref, watch } from "vue";
import { Bar, Line } from "vue-chartjs";
import { useRoute } from "vue-router";

import type { LyricsStatsBucket } from "@/api/lyricsStats";

import { LYRICS_PROVIDER, LYRICS_STATS_BUCKET, LYRICS_STATUS } from "@/api/lyricsStats";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import JobRecoveryPanel from "@/components/dashboard/admin/monitoring/JobRecoveryPanel.vue";
import ChartFrame from "@/components/dashboard/integrations/ChartFrame.vue";
import IntegrationPageHeader from "@/components/dashboard/integrations/IntegrationPageHeader.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import SegmentedControl from "@/components/dashboard/integrations/SegmentedControl.vue";
import StatTile from "@/components/dashboard/integrations/StatTile.vue";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useRollingNow } from "@/composables/useRollingNow";
import { useTheme } from "@/composables/useTheme";
import { JobType, ServiceType } from "@/enums";
import { parseDeepLinkQuery } from "@/features/monitoring/service-routing";
import { lyricsOverview, lyricsTrend } from "@/queries/lyricsStats";

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  PointElement,
  BarElement,
  CategoryScale,
  LinearScale,
);

const route = useRoute();
const { isDark } = useTheme();

// Deep-link seeds (D12)
const feedDateRange = ref<{ from: Date; to: Date } | null>(null);
const initialSeverity = ref<number | null>(null);
const highlightId = ref<string | null>(null);

watch(
  () => route.query,
  () => {
    const params = parseDeepLinkQuery(route.query);
    feedDateRange.value = params.from && params.to ? { from: params.from, to: params.to } : null;
    initialSeverity.value = params.severity ?? null;
    highlightId.value = params.eventId ?? null;
  },
  { immediate: true },
);

// Range / bucket state
const rangeOptions = [
  { label: "24h", value: 24 * 60 * 60 * 1000 },
  { label: "7d", value: 7 * 24 * 60 * 60 * 1000 },
] as const;

const bucketOptions: { label: string; value: LyricsStatsBucket }[] = [
  { label: "Hourly", value: LYRICS_STATS_BUCKET.Hour },
  { label: "Daily", value: LYRICS_STATS_BUCKET.Day },
];

const rangeMs = ref(rangeOptions[0].value);
const bucket = ref<LyricsStatsBucket>(LYRICS_STATS_BUCKET.Hour);

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
} = useQuery(lyricsOverview());

// Overview counts have a fixed key, so they are polled explicitly.
useLiveRefresh(refetchOverview, 60_000);
const { data: trend, isPending: trendLoading } = useQuery(() => lyricsTrend(trendParams.value));

const statusCount = (status: number): number =>
  overview.value?.statusCounts.find((entry) => entry.status === status)?.count ?? 0;

const fetchedTotal = computed(() => overview.value?.fetchedTotal ?? 0);
const failedTotal = computed(() => statusCount(LYRICS_STATUS.FetchFailed));

const windowFailureRate = computed(() => {
  const points = trend.value?.points ?? [];
  const total = points.reduce((sum, p) => sum + p.total, 0);
  const failed = points.reduce((sum, p) => sum + p.failed, 0);
  if (total === 0) return null;
  return Math.round((failed / total) * 1000) / 10;
});

const gridColor = computed(() =>
  isDark.value ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)",
);
const tickColor = computed(() => (isDark.value ? "#9ca3af" : "#6b7280"));

const bucketLabel = (iso: string): string => {
  const date = new Date(iso);
  return bucket.value === LYRICS_STATS_BUCKET.Hour
    ? date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
};

// Attempts per bucket split by terminal outcome
const activityChartData = computed<ChartData<"bar">>(() => ({
  labels: (trend.value?.points ?? []).map((p) => bucketLabel(p.bucketStart)),
  datasets: [
    {
      label: "Fetched",
      data: (trend.value?.points ?? []).map((p) => p.fetched),
      backgroundColor: "#10b981",
      borderRadius: 3,
    },
    {
      label: "No match",
      data: (trend.value?.points ?? []).map((p) => Math.max(0, p.total - p.fetched - p.failed)),
      backgroundColor: "#9ca3af",
      borderRadius: 3,
    },
    {
      label: "Failed",
      data: (trend.value?.points ?? []).map((p) => p.failed),
      backgroundColor: "#dc2626",
      borderRadius: 3,
    },
  ],
}));

const PROVIDER_LABELS: Record<number, string> = {
  [LYRICS_PROVIDER.None]: "Unknown / none",
  [LYRICS_PROVIDER.LrclibPublic]: "LRCLIB (public)",
  [LYRICS_PROVIDER.LrclibLocal]: "LRCLIB (local)",
  [LYRICS_PROVIDER.Musicxmatch]: "Musixmatch",
  [LYRICS_PROVIDER.Manual]: "Manual import",
};

interface ProviderRow {
  failedPct: number;
  label: string;
  total: number;
}

const providerRows = computed<ProviderRow[]>(() =>
  (overview.value?.providers ?? [])
    .filter((row) => row.total > 0)
    .sort((a, b) => b.total - a.total)
    .map((row) => ({
      failedPct: Math.round((row.failed / row.total) * 100),
      label: PROVIDER_LABELS[row.provider] ?? String(row.provider),
      total: row.total,
    })),
);

const STATUS_DISTRIBUTION = [
  { color: "bg-sky-500", label: "Fetched", value: statusCount(LYRICS_STATUS.Fetched) },
  {
    color: "bg-gray-400",
    label: "No match",
    value: statusCount(LYRICS_STATUS.NoMatch),
  },
  {
    color: "bg-red-500",
    label: "Failed",
    value: statusCount(LYRICS_STATUS.FetchFailed),
  },
  {
    color: "bg-amber-500",
    label: "Pending",
    value: statusCount(LYRICS_STATUS.PendingFetch),
  },
  {
    color: "bg-indigo-500",
    label: "Fetching",
    value: statusCount(LYRICS_STATUS.Fetching),
  },
];

const distributionTotal = computed(() =>
  STATUS_DISTRIBUTION.reduce((sum, entry) => sum + entry.value, 0),
);

const activityOptions = computed<ChartOptions<"bar">>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: true, labels: { color: tickColor.value } },
    tooltip: { intersect: false, mode: "index" as const },
  },
  scales: {
    x: {
      stacked: true,
      grid: { color: gridColor.value },
      ticks: { color: tickColor.value, maxRotation: 0 },
    },
    y: {
      stacked: true,
      beginAtZero: true,
      grid: { color: gridColor.value },
      ticks: { color: tickColor.value, precision: 0 },
    },
  },
}));

const baseOptions = computed<ChartOptions<any>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  indexAxis: "y" as const,
  plugins: { legend: { display: false } },
  scales: {
    x: {
      beginAtZero: true,
      grid: { color: gridColor.value },
      ticks: { color: tickColor.value, precision: 0 },
    },
    y: { grid: { color: gridColor.value }, ticks: { color: tickColor.value } },
  },
}));
</script>

<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <IntegrationPageHeader
      title="Lyrics"
      icon="mdi:script-text-outline"
      :services="[ServiceType.Lyrics]"
    >
      <template #actions>
        <SegmentedControl v-model="rangeMs" :options="rangeOptions" label="Time range" />
        <SegmentedControl v-model="bucket" :options="bucketOptions" label="Bucket size" />
      </template>
    </IntegrationPageHeader>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="flex flex-col gap-3">
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatTile label="Fetched" :loading="overviewLoading">
            {{ fetchedTotal.toLocaleString() }}
          </StatTile>
          <StatTile
            label="Failed fetches"
            :loading="overviewLoading"
            :tone="failedTotal > 0 ? 'danger' : 'default'"
          >
            {{ failedTotal.toLocaleString() }}
          </StatTile>
          <StatTile label="Failure rate (window)" :loading="trendLoading">
            {{ windowFailureRate === null ? "—" : `${windowFailureRate}%` }}
          </StatTile>
          <!-- ConfidenceScore is an arbitrary 1–10 scale, not a 0–1 ratio. -->
          <StatTile label="Avg confidence" :loading="overviewLoading">
            {{
              overview?.avgConfidence == null ? "—" : `${overview.avgConfidence.toFixed(1)} / 10`
            }}
          </StatTile>
        </div>

        <IntegrationPanel title="Fetch attempts by outcome">
          <ChartFrame :loading="trendLoading">
            <Bar :data="activityChartData" :options="activityOptions" />
          </ChartFrame>
        </IntegrationPanel>

        <div class="grid grid-cols-1 gap-3 md:grid-cols-2">
          <IntegrationPanel title="Status distribution">
            <div v-if="overviewLoading" class="flex justify-center py-12">
              <BlockSpinner
                :size="24"
                aria-label="Loading status distribution"
                class="text-gray-500"
              />
            </div>
            <div
              v-else-if="distributionTotal === 0"
              class="flex flex-col items-center justify-center gap-2 py-8 text-center"
            >
              <UIcon name="i-lucide-music" class="h-10 w-10 text-gray-400 dark:text-gray-600" />
              <p class="text-sm text-gray-600 dark:text-gray-400">No lyrics tracked yet.</p>
            </div>
            <div v-else class="flex flex-col gap-2.5">
              <div
                v-for="entry in STATUS_DISTRIBUTION"
                :key="entry.label"
                class="flex items-center gap-3"
              >
                <span class="w-20 shrink-0 text-xs text-gray-600 dark:text-gray-400">
                  {{ entry.label }}
                </span>
                <div class="h-2.5 flex-1 overflow-hidden rounded-full bg-gray-500/15">
                  <div
                    class="h-full rounded-full"
                    :class="entry.color"
                    :style="{ width: `${(entry.value / distributionTotal) * 100}%` }"
                  />
                </div>
                <span
                  class="w-12 shrink-0 text-right text-xs font-semibold tabular-nums text-gray-900 dark:text-gray-100"
                >
                  {{ entry.value.toLocaleString() }}
                </span>
              </div>
            </div>
          </IntegrationPanel>

          <IntegrationPanel title="Providers">
            <div v-if="overviewLoading" class="flex justify-center py-12">
              <BlockSpinner :size="24" aria-label="Loading providers" class="text-gray-500" />
            </div>
            <div
              v-else-if="providerRows.length === 0"
              class="flex flex-col items-center justify-center gap-2 py-8 text-center"
            >
              <UIcon name="i-lucide-search" class="h-10 w-10 text-gray-400 dark:text-gray-600" />
              <p class="text-sm text-gray-600 dark:text-gray-400">No fetch attempts yet.</p>
            </div>
            <div v-else class="flex flex-col gap-3">
              <div v-for="row in providerRows" :key="row.label" class="flex flex-col gap-1">
                <div class="flex items-center justify-between gap-2">
                  <span class="truncate text-xs font-medium text-gray-900 dark:text-gray-100">
                    {{ row.label }}
                  </span>
                  <span
                    class="shrink-0 text-xs tabular-nums"
                    :class="
                      row.failedPct > 25
                        ? 'text-red-600 dark:text-red-400'
                        : 'text-gray-600 dark:text-gray-400'
                    "
                  >
                    {{ row.failedPct }}% failed · {{ row.total }} attempts
                  </span>
                </div>
                <div class="h-1.5 overflow-hidden rounded-full bg-gray-500/15">
                  <div
                    class="h-full rounded-full"
                    :class="row.failedPct > 25 ? 'bg-red-500' : 'bg-emerald-500'"
                    :style="{ width: `${row.failedPct}%` }"
                  />
                </div>
              </div>
            </div>
          </IntegrationPanel>
        </div>

        <JobRecoveryPanel :type="JobType.LyricsFetch" title="Lyrics jobs" />

        <section aria-labelledby="lyrics-incidents" class="flex flex-col gap-2">
          <h2
            id="lyrics-incidents"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            Incidents
          </h2>
          <EventsFeed
            :date-range="feedDateRange"
            :initial-service-type="ServiceType.Lyrics"
            :initial-severity="initialSeverity"
            :highlight-id="highlightId"
            locked-service
          />
        </section>
      </div>
    </div>
  </div>
</template>
