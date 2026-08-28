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
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import { useTheme } from "@/composables/useTheme";
import { ServiceType } from "@/enums";
import { lyricsOverview, lyricsTrend } from "@/queries/lyricsStats";
import { parseDeepLinkQuery } from "@/utils/serviceDashboardRouting";

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

const trendParams = computed(() => {
  const to = Date.now();
  return {
    from: new Date(to - rangeMs.value).toISOString(),
    to: new Date(to).toISOString(),
    bucket: bucket.value,
  };
});

const { data: overview, isLoading: overviewLoading } = useQuery(lyricsOverview());
const { data: trend, isLoading: trendLoading } = useQuery(lyricsTrend, () => trendParams.value);

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
  <div class="p-3 sm:p-5 lg:p-6">
    <div class="max-w-400 mx-auto space-y-3 lg:space-y-4">
      <!-- Page header -->
      <div class="flex items-center justify-between gap-3 flex-wrap mb-1">
        <div>
          <h1 class="text-lg font-semibold text-gray-800 dark:text-gray-100">Lyrics Dashboard</h1>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
            Fetch outcomes, provider reliability and confidence for the lyrics worker
          </p>
        </div>
      </div>

      <!-- Stat cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Fetched
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ fetchedTotal.toLocaleString() }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Failed fetches
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p
            v-else
            class="text-2xl font-bold tabular-nums"
            :class="
              failedTotal > 0
                ? 'text-red-600 dark:text-red-400'
                : 'text-gray-900 dark:text-gray-100'
            "
          >
            {{ failedTotal.toLocaleString() }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Failure rate (window)
          </p>
          <USkeleton v-if="trendLoading" class="h-7 w-16 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ windowFailureRate === null ? "—" : `${windowFailureRate}%` }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Avg confidence
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-16 mt-1" />
          <!-- ConfidenceScore is an arbitrary 1–10 scale (matches the
               LyricsPanel badge), not a 0–1 ratio -->
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{
              overview?.avgConfidence == null ? "—" : `${overview.avgConfidence.toFixed(1)} / 10`
            }}
          </p>
        </div>
      </div>

      <!-- Charts -->
      <div class="space-y-3">
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
          <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">
            Fetch attempts by outcome
          </p>
          <!-- Bounded height is mandatory with maintainAspectRatio:false -->
          <div v-if="trendLoading" class="h-56">
            <USkeleton class="h-full w-full rounded-xl" />
          </div>
          <div v-else class="relative h-56">
            <Bar :data="activityChartData" :options="activityOptions" />
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
          <!-- Status distribution -->
          <div
            class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4"
          >
            <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">
              Status distribution
            </p>
            <USkeleton v-if="overviewLoading" class="h-40 w-full rounded-xl" />
            <template v-else>
              <div
                v-if="distributionTotal === 0"
                class="flex flex-col items-center justify-center py-8 text-center"
              >
                <UIcon
                  name="i-mdi-music-note-outline"
                  class="w-8 h-8 text-gray-400 dark:text-gray-600 mb-2"
                />
                <p class="text-xs text-gray-500 dark:text-gray-400">No lyrics tracked yet.</p>
              </div>
              <div v-else class="space-y-2">
                <div
                  v-for="entry in STATUS_DISTRIBUTION"
                  :key="entry.label"
                  class="flex items-center gap-3"
                >
                  <span class="w-20 text-xs text-gray-500 dark:text-gray-400 shrink-0">{{
                    entry.label
                  }}</span>
                  <div
                    class="flex-1 h-2.5 rounded-full bg-black/5 dark:bg-white/10 overflow-hidden"
                  >
                    <div
                      class="h-full rounded-full transition-all duration-500"
                      :class="entry.color"
                      :style="{
                        width: `${distributionTotal ? (entry.value / distributionTotal) * 100 : 0}%`,
                      }"
                    />
                  </div>
                  <span
                    class="w-12 text-right text-xs font-semibold tabular-nums text-gray-700 dark:text-gray-200 shrink-0"
                  >
                    {{ entry.value.toLocaleString() }}
                  </span>
                </div>
              </div>
            </template>
          </div>

          <!-- Provider breakdown -->
          <div
            class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4"
          >
            <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Providers</p>
            <USkeleton v-if="overviewLoading" class="h-40 w-full rounded-xl" />
            <div
              v-else-if="providerRows.length === 0"
              class="flex flex-col items-center justify-center py-8 text-center"
            >
              <UIcon name="i-mdi-magnify" class="w-8 h-8 text-gray-400 dark:text-gray-600 mb-2" />
              <p class="text-xs text-gray-500 dark:text-gray-400">No fetch attempts yet.</p>
            </div>
            <div v-else class="space-y-2">
              <div v-for="row in providerRows" :key="row.label" class="space-y-1">
                <div class="flex items-center justify-between gap-2">
                  <span class="text-xs font-medium text-gray-700 dark:text-gray-300 truncate">
                    {{ row.label }}
                  </span>
                  <span
                    class="text-[11px] tabular-nums shrink-0"
                    :class="
                      row.failedPct > 25
                        ? 'text-red-600 dark:text-red-400'
                        : 'text-gray-500 dark:text-gray-400'
                    "
                  >
                    {{ row.failedPct }}% failed · {{ row.total }} attempts
                  </span>
                </div>
                <div class="h-1.5 rounded-full bg-black/5 dark:bg-white/10 overflow-hidden">
                  <div
                    class="h-full rounded-full"
                    :class="row.failedPct > 25 ? 'bg-red-500' : 'bg-emerald-500'"
                    :style="{ width: `${row.failedPct}%` }"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Incidents -->
      <div class="space-y-2">
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300 px-1">
          Lyrics incidents
        </h2>
        <EventsFeed
          :date-range="feedDateRange"
          :initial-service-type="ServiceType.Lyrics"
          :initial-severity="initialSeverity"
          :highlight-id="highlightId"
          locked-service
        />
      </div>
    </div>
  </div>
</template>
