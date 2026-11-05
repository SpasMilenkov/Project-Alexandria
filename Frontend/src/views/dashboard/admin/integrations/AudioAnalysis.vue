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

import type { EnrichmentBucket } from "@/api/audioAnalysis";

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
import {
  batches,
  duration,
  failureRate,
  queueDepth,
  stuck,
  volumeByHour,
} from "@/queries/audioAnalysis";
import { formatDate, formatDuration } from "@/utils/date-formatters";

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

const { isDark } = useTheme();

// Incidents section (D12): consumes the shared deep-link params so doorways
// from the monitoring surfaces land pre-filtered on this page.
const route = useRoute();
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

const palette = ["#C17B5C", "#5B7FA6", "#7A9E87", "#C9A84C"];

// Single canonical color per backbone, reused everywhere (queue dots, duration
// cards, and the failure-rate chart) so a given backbone reads the same
// color across the whole page instead of drifting based on array order.
const backboneColorMap: Record<string, string> = {
  Effnet: "#C17B5C",
  Maest: "#5B7FA6",
};
const backboneColor = (backbone: string, fallbackIndex = 0) =>
  backboneColorMap[backbone] ?? palette[fallbackIndex % palette.length];

const rangeOptions = [
  { label: "24h", value: 24 * 60 * 60 * 1000 },
  { label: "7d", value: 7 * 24 * 60 * 60 * 1000 },
] as const;

const bucketOptions: { label: string; value: EnrichmentBucket }[] = [
  { label: "Hourly", value: "hour" },
  { label: "Daily", value: "day" },
];

const rangeMs = ref(rangeOptions[0].value);
const bucket = ref<EnrichmentBucket>("hour");

// Minute ticks move the trailing window forward; Refresh ticks it immediately.
const { now, tick } = useRollingNow();

const rangeDates = computed(() => {
  const to = now.value;
  const from = to - rangeMs.value;
  return {
    from: new Date(from).toISOString(),
    to: new Date(to).toISOString(),
  };
});

const failureRange = computed(() => ({ ...rangeDates.value, bucket: bucket.value }));
const volumeRange = computed(() => rangeDates.value);

const queue = useQuery(queueDepth());
const stuckQuery = useQuery(stuck());
const failure = useQuery(() => failureRate(failureRange.value));
const durationQuery = useQuery(duration());
const volume = useQuery(() => volumeByHour(volumeRange.value));
const recent = useQuery(() => batches(20));

const isRefreshing = computed(
  () =>
    queue.asyncStatus.value === "loading" ||
    stuckQuery.asyncStatus.value === "loading" ||
    failure.asyncStatus.value === "loading" ||
    durationQuery.asyncStatus.value === "loading" ||
    volume.asyncStatus.value === "loading" ||
    recent.asyncStatus.value === "loading",
);

const hasError = computed(
  () =>
    queue.status.value === "error" ||
    stuckQuery.status.value === "error" ||
    failure.status.value === "error" ||
    durationQuery.status.value === "error" ||
    volume.status.value === "error" ||
    recent.status.value === "error",
);

const handleRefresh = () => {
  tick();
  queue.refetch();
  stuckQuery.refetch();
  failure.refetch();
  durationQuery.refetch();
  volume.refetch();
  recent.refetch();
};

useLiveRefresh(() => {
  queue.refetch();
  stuckQuery.refetch();
}, 30_000);

// Tracks when the last successful refresh finished, so the header can show a
// quiet "Updated 2:41 PM" instead of leaving the user guessing about staleness.
const lastUpdated = ref(new Date());
const timeOnly = new Intl.DateTimeFormat(undefined, { hour: "2-digit", minute: "2-digit" });
watch(isRefreshing, (loading, wasLoading) => {
  if (wasLoading && !loading) {
    lastUpdated.value = new Date();
  }
});

const shortId = (id: string) => id.slice(0, 8).toUpperCase();

const fileStatusMeta: Record<string, { dot: string; label: string }> = {
  Queued: { dot: "bg-amber-500", label: "Queued" },
  Processing: { dot: "bg-blue-500", label: "Processing" },
  Partial: { dot: "bg-amber-400", label: "Partial" },
  Ready: { dot: "bg-emerald-500", label: "Ready" },
  Failed: { dot: "bg-red-500", label: "Failed" },
  Cancelled: { dot: "bg-gray-400", label: "Cancelled" },
  CancellationRequested: { dot: "bg-orange-400", label: "Cancelling" },
};

const batchStatusMeta: Record<string, { bg: string; label: string; text: string }> = {
  Completed: {
    bg: "bg-emerald-500/10",
    label: "Completed",
    text: "text-emerald-600 dark:text-emerald-400",
  },
  Dispatched: {
    bg: "bg-amber-500/10",
    label: "In progress",
    text: "text-amber-600 dark:text-amber-400",
  },
  TimedOut: {
    bg: "bg-red-500/10",
    label: "Timed out",
    text: "text-red-600 dark:text-red-400",
  },
};

const queueByBackbone = computed(() => {
  const rows = queue.data.value ?? [];
  const byBackbone = new Map<string, { backbone: string; rows: typeof rows }>();
  for (const row of rows) {
    const group = byBackbone.get(row.backbone);
    if (group) {
      group.rows.push(row);
    } else {
      byBackbone.set(row.backbone, { backbone: row.backbone, rows: [row] });
    }
  }
  return Array.from(byBackbone.values());
});

const queueTotal = computed(() =>
  (queue.data.value ?? []).reduce((sum, row) => sum + row.count, 0),
);

// ---- KPI summary strip ----------------------------------------------------
// Gives the page one clear "is anything wrong right now" read before anyone
// has to scan six separate sections.

const stuckTotal = computed(() => (stuckQuery.data.value ?? []).length);

const overallFailureRate = computed(() => {
  const points = failure.data.value ?? [];
  if (!points.length) return null;
  const totals = points.reduce(
    (acc, p) => {
      acc.failed += p.failed;
      acc.total += p.total;
      return acc;
    },
    { failed: 0, total: 0 },
  );
  if (totals.total === 0) return null;
  return (totals.failed / totals.total) * 100;
});

const overallAvgDuration = computed(() => {
  const rows = durationQuery.data.value ?? [];
  if (!rows.length) return null;
  const sum = rows.reduce((acc, row) => acc + row.averageSeconds, 0);
  return sum / rows.length;
});

const kpiTiles = computed(() => [
  {
    key: "inFlight",
    icon: "i-lucide-audio-lines",
    label: "In flight",
    value: `${queueTotal.value}`,
    tone: "neutral" as const,
  },
  {
    key: "stuck",
    icon: "mdi:clock-alert-outline",
    label: "Stuck files",
    value: `${stuckTotal.value}`,
    tone: stuckTotal.value > 0 ? ("warning" as const) : ("neutral" as const),
  },
  {
    key: "failureRate",
    icon: "mdi:chart-line",
    label: `Failure rate (${rangeOptions.find((r) => r.value === rangeMs.value)?.label})`,
    value: overallFailureRate.value === null ? "—" : `${overallFailureRate.value.toFixed(1)}%`,
    tone:
      overallFailureRate.value !== null && overallFailureRate.value >= 10
        ? ("warning" as const)
        : ("neutral" as const),
  },
  {
    key: "avgDuration",
    icon: "mdi:timer-outline",
    label: "Avg duration",
    value: overallAvgDuration.value === null ? "—" : formatDuration(overallAvgDuration.value),
    tone: "neutral" as const,
  },
]);

const shortTime = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  month: "short",
});

const shortDay = new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short" });

const formatBucketLabel = (iso: string) =>
  bucket.value === "day" ? shortDay.format(new Date(iso)) : shortTime.format(new Date(iso));

const formatHourLabel = (iso: string) => shortTime.format(new Date(iso));

const failureChartData = computed((): ChartData<"line"> => {
  const points = failure.data.value ?? [];
  const backbones = Array.from(new Set(points.map((p) => p.backbone)));
  const buckets = Array.from(new Set(points.map((p) => p.bucket))).sort();
  const byBackbone = new Map<string, Map<string, number>>();
  for (const point of points) {
    let series = byBackbone.get(point.backbone);
    if (!series) {
      series = new Map<string, number>();
      byBackbone.set(point.backbone, series);
    }
    series.set(point.bucket, Number((point.failureRate * 100).toFixed(1)));
  }
  return {
    datasets: backbones.map((backbone, i) => {
      const color = backboneColor(backbone, i);
      return {
        borderColor: color,
        data: buckets.map((b) => byBackbone.get(backbone)?.get(b) ?? 0),
        label: backbone,
        pointBackgroundColor: color,
        pointRadius: 3,
        pointHoverRadius: 5,
        tension: 0.3,
      };
    }),
    labels: buckets.map(formatBucketLabel),
  };
});

const failureEmpty = computed(() => (failure.data.value ?? []).length === 0);

const failureChartOptions = computed((): ChartOptions<"line"> => ({
  maintainAspectRatio: false,
  plugins: {
    legend: {
      labels: {
        boxWidth: 8,
        color: isDark.value ? "#999" : "#666",
        usePointStyle: true,
      },
    },
    tooltip: {
      backgroundColor: isDark.value ? "#1e1e1e" : "#ffffff",
      bodyColor: isDark.value ? "#999" : "#666",
      borderColor: isDark.value ? "#333" : "#e5e5e5",
      borderWidth: 1,
      callbacks: {
        label: (context) => `  ${context.dataset.label}: ${context.parsed.y}%`,
      },
      padding: 10,
      titleColor: isDark.value ? "#e0ddd8" : "#1a1a1a",
    },
  },
  responsive: true,
  scales: {
    x: {
      grid: { color: isDark.value ? "rgba(255,255,255,0.06)" : "rgba(0,0,0,0.05)" },
      ticks: {
        color: isDark.value ? "#999" : "#666",
        maxRotation: 45,
        maxTicksLimit: 8,
      },
    },
    y: {
      beginAtZero: true,
      grid: { color: isDark.value ? "rgba(255,255,255,0.06)" : "rgba(0,0,0,0.05)" },
      max: 100,
      ticks: {
        callback: (value) => `${value}%`,
        color: isDark.value ? "#999" : "#666",
      },
    },
  },
}));

// Volume is a single time series, not a category breakdown, so every bar
// shares one color rather than cycling through the palette per-bar. The
// per-bar rainbow was decorative, not informational.
const volumeBarColor = "#5B7FA6";

const volumeChartData = computed((): ChartData<"bar"> => {
  const points = volume.data.value ?? [];
  return {
    datasets: [
      {
        backgroundColor: isDark.value ? `${volumeBarColor}cc` : `${volumeBarColor}b3`,
        borderColor: isDark.value ? "#1a1a1a" : "#f8f7f4",
        borderWidth: 1,
        borderRadius: 3,
        data: points.map((p) => p.count),
        hoverBackgroundColor: volumeBarColor,
        label: "Files",
      },
    ],
    labels: points.map((p) => formatHourLabel(p.hour)),
  };
});

const volumeEmpty = computed(() => (volume.data.value ?? []).length === 0);

const volumeChartOptions = computed((): ChartOptions<"bar"> => ({
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: isDark.value ? "#1e1e1e" : "#ffffff",
      bodyColor: isDark.value ? "#999" : "#666",
      borderColor: isDark.value ? "#333" : "#e5e5e5",
      borderWidth: 1,
      padding: 10,
      titleColor: isDark.value ? "#e0ddd8" : "#1a1a1a",
    },
  },
  responsive: true,
  scales: {
    x: {
      grid: { display: false },
      ticks: {
        color: isDark.value ? "#999" : "#666",
        maxRotation: 45,
        maxTicksLimit: 8,
      },
    },
    y: {
      beginAtZero: true,
      grid: { color: isDark.value ? "rgba(255,255,255,0.06)" : "rgba(0,0,0,0.05)" },
      ticks: {
        precision: 0,
        color: isDark.value ? "#999" : "#666",
      },
    },
  },
}));

const stuckSorted = computed(() => [...(stuckQuery.data.value ?? [])].reverse());

const queueHint = computed(
  () => `${queueTotal.value} file${queueTotal.value === 1 ? "" : "s"} in flight`,
);

const durationStats = (row: {
  averageSeconds: number;
  medianSeconds: number;
  maxSeconds: number;
}) => [
  { label: "Average", value: formatDuration(row.averageSeconds) },
  { label: "Median", value: formatDuration(row.medianSeconds) },
  { label: "Max", value: formatDuration(row.maxSeconds) },
];

const batchProgress = (batch: { filesCompleted: number; filesTotal: number }) => {
  if (batch.filesTotal <= 0) return "0%";

  return `${Math.round((batch.filesCompleted / batch.filesTotal) * 100)}%`;
};

const BATCH_COLUMNS = [
  { align: "", label: "Backbone" },
  { align: "", label: "Status" },
  { align: "text-right", label: "Progress" },
  { align: "text-right", label: "Created" },
  { align: "text-right", label: "Completed" },
];
</script>

<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <IntegrationPageHeader
      title="Audio analysis"
      icon="i-lucide-audio-lines"
      :services="[ServiceType.MediaMetadata]"
    >
      <template #actions>
        <span class="hidden text-xs text-gray-600 lg:inline dark:text-gray-400">
          Updated {{ timeOnly.format(lastUpdated) }}
        </span>
        <SegmentedControl v-model="rangeMs" :options="rangeOptions" label="Time range" />
        <SegmentedControl v-model="bucket" :options="bucketOptions" label="Bucket size" />
        <UTooltip text="Refresh">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-refresh-cw"
            square
            class="justify-center"
            aria-label="Refresh"
            :loading="isRefreshing"
            @click="handleRefresh()"
          />
        </UTooltip>
      </template>
    </IntegrationPageHeader>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="flex flex-col gap-3">
        <UAlert
          v-if="hasError"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Could not reach the monitoring data"
          description="Some sections may be stale or empty. The server may be offline."
        />

        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatTile
            v-for="tile in kpiTiles"
            :key="tile.key"
            :label="tile.label"
            :icon="tile.icon"
            :tone="tile.tone === 'warning' ? 'warning' : 'default'"
          >
            {{ tile.value }}
          </StatTile>
        </div>

        <div class="grid grid-cols-1 gap-3 xl:grid-cols-2">
          <IntegrationPanel title="Queue depth" :hint="queueHint" flush>
            <div v-if="queue.status.value === 'pending'" class="flex justify-center py-12">
              <BlockSpinner :size="24" aria-label="Loading queue depth" class="text-gray-500" />
            </div>

            <div
              v-else-if="queueByBackbone.length"
              class="grid grid-cols-1 divide-y divide-gray-200/70 md:grid-cols-2 md:divide-x md:divide-y-0 dark:divide-gray-700/70"
            >
              <div v-for="group in queueByBackbone" :key="group.backbone">
                <p
                  class="flex items-center gap-2 border-b border-gray-200/70 px-4 py-2.5 text-sm font-semibold text-gray-900 sm:px-5 dark:border-gray-700/70 dark:text-gray-100"
                >
                  <span
                    class="h-2 w-2 shrink-0 rounded-full"
                    :style="{ background: backboneColor(group.backbone) }"
                  />
                  {{ group.backbone }}
                  <span class="ml-auto text-xs font-normal text-gray-600 dark:text-gray-400">
                    {{ group.rows.reduce((sum, row) => sum + row.count, 0) }} queued
                  </span>
                </p>
                <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
                  <li
                    v-for="row in group.rows"
                    :key="row.status"
                    class="flex items-center gap-3 px-4 py-2.5 sm:px-5"
                  >
                    <span
                      class="h-2 w-2 shrink-0 rounded-full"
                      :class="fileStatusMeta[row.status]?.dot"
                    />
                    <span class="text-sm text-gray-600 dark:text-gray-400">
                      {{ fileStatusMeta[row.status]?.label ?? row.status }}
                    </span>
                    <span
                      class="ml-auto text-sm font-semibold tabular-nums text-gray-900 dark:text-gray-100"
                    >
                      {{ row.count }}
                    </span>
                  </li>
                </ul>
              </div>
            </div>

            <div v-else class="flex flex-col items-center gap-2 px-6 py-12 text-center">
              <UIcon
                name="i-lucide-audio-lines"
                class="h-10 w-10 text-gray-400 dark:text-gray-600"
              />
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                No batches in flight
              </p>
              <p class="max-w-sm text-xs text-gray-600 dark:text-gray-400">
                Files currently being analyzed will appear here as they are dispatched to the
                worker.
              </p>
            </div>
          </IntegrationPanel>

          <IntegrationPanel title="Stuck files" hint="Pending past the server threshold" flush>
            <div v-if="stuckQuery.status.value === 'pending'" class="flex justify-center py-12">
              <BlockSpinner :size="24" aria-label="Loading stuck files" class="text-gray-500" />
            </div>

            <ul
              v-else-if="stuckSorted.length"
              class="divide-y divide-gray-200/70 dark:divide-gray-700/70"
            >
              <li
                v-for="row in stuckSorted"
                :key="row.fileId"
                class="flex items-center gap-3 px-4 py-3 sm:px-5"
              >
                <UIcon name="i-lucide-alert-circle" class="h-4 w-4 shrink-0 text-amber-500" />
                <div class="min-w-0 flex-1">
                  <p class="font-mono text-sm font-medium text-gray-900 dark:text-gray-100">
                    {{ shortId(row.fileId) }}
                  </p>
                  <p class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
                    Batch {{ shortId(row.batchId) }}
                    <span
                      class="inline-block h-1.5 w-1.5 rounded-full"
                      :style="{ background: backboneColor(row.backbone) }"
                    />
                    {{ row.backbone }}
                  </p>
                </div>
                <span class="shrink-0 text-xs text-gray-600 dark:text-gray-400">
                  {{ formatDate(row.createdAt) }}
                </span>
              </li>
            </ul>

            <div v-else class="flex flex-col items-center gap-2 px-6 py-12 text-center">
              <UIcon
                name="i-lucide-circle-check"
                class="h-10 w-10 text-gray-400 dark:text-gray-600"
              />
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No stuck files</p>
              <p class="max-w-sm text-xs text-gray-600 dark:text-gray-400">
                No files have been pending longer than the configured threshold.
              </p>
            </div>
          </IntegrationPanel>
        </div>

        <IntegrationPanel title="Failure rate" hint="Per backbone">
          <ChartFrame
            height="lg"
            :loading="failure.status.value === 'pending'"
            :empty="failureEmpty"
            empty-text="Completed analyses in the selected range will show failure rates per backbone."
          >
            <Line :data="failureChartData" :options="failureChartOptions" />
          </ChartFrame>
        </IntegrationPanel>

        <div class="grid grid-cols-1 gap-3 xl:grid-cols-2">
          <IntegrationPanel title="Durations" hint="Completed at minus created at" flush>
            <div v-if="durationQuery.status.value === 'pending'" class="flex justify-center py-12">
              <BlockSpinner :size="24" aria-label="Loading durations" class="text-gray-500" />
            </div>

            <ul
              v-else-if="(durationQuery.data.value ?? []).length"
              class="divide-y divide-gray-200/70 dark:divide-gray-700/70"
            >
              <li
                v-for="row in durationQuery.data.value"
                :key="row.backbone"
                class="px-4 py-3 sm:px-5"
              >
                <p
                  class="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
                >
                  <span
                    class="h-2 w-2 shrink-0 rounded-full"
                    :style="{ background: backboneColor(row.backbone) }"
                  />
                  {{ row.backbone }}
                </p>
                <dl class="grid grid-cols-3 gap-3">
                  <div v-for="stat in durationStats(row)" :key="stat.label">
                    <dt class="text-xs text-gray-600 dark:text-gray-400">{{ stat.label }}</dt>
                    <dd class="text-lg font-semibold tabular-nums text-gray-900 dark:text-gray-100">
                      {{ stat.value }}
                    </dd>
                  </div>
                </dl>
              </li>
            </ul>

            <div v-else class="flex flex-col items-center gap-2 px-6 py-12 text-center">
              <UIcon name="i-lucide-clock" class="h-10 w-10 text-gray-400 dark:text-gray-600" />
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No durations yet</p>
              <p class="max-w-sm text-xs text-gray-600 dark:text-gray-400">
                Once analyses finish, average, median and max processing times appear per backbone.
              </p>
            </div>
          </IntegrationPanel>

          <IntegrationPanel title="Volume by hour" hint="Analyses created per hour">
            <ChartFrame
              :loading="volume.status.value === 'pending'"
              :empty="volumeEmpty"
              empty-title="No volume in this range"
              empty-text="Hourly analysis volume for the selected period will appear here."
            >
              <Bar :data="volumeChartData" :options="volumeChartOptions" />
            </ChartFrame>
          </IntegrationPanel>
        </div>

        <IntegrationPanel title="Recent batches" hint="Latest 20" flush>
          <div v-if="recent.status.value === 'pending'" class="flex justify-center py-12">
            <BlockSpinner :size="24" aria-label="Loading recent batches" class="text-gray-500" />
          </div>

          <div v-else-if="(recent.data.value ?? []).length">
            <div
              class="hidden grid-cols-[1fr_90px_110px_100px_100px] items-center gap-4 border-b border-gray-200/70 px-5 py-2.5 md:grid dark:border-gray-700/70"
            >
              <span
                v-for="column in BATCH_COLUMNS"
                :key="column.label"
                class="text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
                :class="column.align"
              >
                {{ column.label }}
              </span>
            </div>
            <!--
              One row template, not two. Named grid areas hold identity / status /
              progress / created / completed; only the areas and columns change at md.
            -->
            <div
              v-for="(batch, index) in recent.data.value"
              :key="batch.id"
              class="grid grid-cols-4 items-center gap-x-3 gap-y-2 px-4 py-3 [grid-template-areas:'identity_identity_status_status'_'progress_progress_progress_progress'_'created_created_completed_completed'] md:grid-cols-[1fr_90px_110px_100px_100px] md:gap-4 md:px-5 md:[grid-template-areas:'identity_status_progress_created_completed']"
              :class="{ 'border-t border-gray-200/70 dark:border-gray-700/70': index > 0 }"
            >
              <div class="flex min-w-0 items-center gap-2 [grid-area:identity]">
                <span
                  class="h-2 w-2 shrink-0 rounded-full"
                  :style="{ background: backboneColor(batch.backbone) }"
                />
                <div class="min-w-0">
                  <p
                    class="truncate font-mono text-sm font-medium text-gray-900 dark:text-gray-100"
                  >
                    {{ shortId(batch.id) }}
                  </p>
                  <p class="text-xs text-gray-600 dark:text-gray-400">{{ batch.backbone }}</p>
                </div>
              </div>

              <span
                class="justify-self-end rounded-full px-2.5 py-1 text-xs font-semibold [grid-area:status] md:justify-self-start"
                :class="[
                  batchStatusMeta[batch.status]?.bg ?? 'bg-gray-500/10',
                  batchStatusMeta[batch.status]?.text ?? 'text-gray-600 dark:text-gray-400',
                ]"
              >
                {{ batchStatusMeta[batch.status]?.label ?? batch.status }}
              </span>

              <div class="flex items-center gap-2 [grid-area:progress] md:justify-self-end">
                <div
                  class="h-1 flex-1 overflow-hidden rounded-full bg-gray-500/15 md:w-12 md:flex-none"
                >
                  <div
                    class="h-full rounded-full bg-gray-600 dark:bg-gray-400"
                    :style="{ width: batchProgress(batch) }"
                  />
                </div>
                <span class="shrink-0 text-xs tabular-nums text-gray-600 dark:text-gray-400">
                  {{ batch.filesCompleted }} / {{ batch.filesTotal }}
                </span>
              </div>

              <span
                class="text-xs text-gray-600 [grid-area:created] md:text-right dark:text-gray-400"
              >
                <span class="md:hidden">Created </span>{{ formatDate(batch.createdAt) }}
              </span>

              <span
                class="text-right text-xs text-gray-600 [grid-area:completed] dark:text-gray-400"
              >
                <span class="md:hidden">Completed </span
                >{{ batch.completedAt ? formatDate(batch.completedAt) : "—" }}
              </span>
            </div>
          </div>

          <div v-else class="flex flex-col items-center gap-2 px-6 py-12 text-center">
            <UIcon name="i-lucide-history" class="h-10 w-10 text-gray-400 dark:text-gray-600" />
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No batches yet</p>
            <p class="max-w-sm text-xs text-gray-600 dark:text-gray-400">
              Batches dispatched to the audio analysis worker will appear here.
            </p>
          </div>
        </IntegrationPanel>

        <JobRecoveryPanel :type="JobType.MetadataEnrichment" title="Enrichment jobs" />

        <!-- Locked to MediaMetadata and fed by the shared deep-link contract. -->
        <section aria-labelledby="audio-analysis-incidents" class="flex flex-col gap-2">
          <h2
            id="audio-analysis-incidents"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            Incidents
          </h2>
          <EventsFeed
            :date-range="feedDateRange"
            :initial-severity="initialSeverity"
            :highlight-id="highlightId"
            :initial-service-type="ServiceType.MediaMetadata"
            locked-service
          />
        </section>
      </div>
    </div>
  </div>
</template>
