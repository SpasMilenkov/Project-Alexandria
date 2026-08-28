<script setup lang="ts">
import { Icon } from "@iconify/vue";
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

import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import { useTheme } from "@/composables/useTheme";
import { ServiceType } from "@/enums";
import {
  batches,
  duration,
  failureRate,
  queueDepth,
  stuck,
  volumeByHour,
} from "@/queries/audioAnalysis";
import { formatDate, formatDuration } from "@/utils/date-formatters";
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

const rangeDates = computed(() => {
  const to = Date.now();
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
  queue.refresh();
  stuckQuery.refresh();
  failure.refresh();
  durationQuery.refresh();
  volume.refresh();
  recent.refresh();
};

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
  Failed: { dot: "bg-red-500", label: "Failed" },
  MissingOutput: { dot: "bg-red-400", label: "Missing output" },
  Pending: { dot: "bg-amber-500", label: "Pending" },
  Succeeded: { dot: "bg-emerald-500", label: "Succeeded" },
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
    icon: "mdi:waveform",
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

const failureChartOptions = computed(
  (): ChartOptions<"line"> => ({
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
  }),
);

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

const volumeChartOptions = computed(
  (): ChartOptions<"bar"> => ({
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
  }),
);

const stuckSorted = computed(() => [...(stuckQuery.data.value ?? [])].reverse());
</script>

<template>
  <div class="flex flex-col gap-6 p-6 max-w-7xl mx-auto w-full">
    <header class="flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1
          class="font-playfair text-3xl font-bold tracking-tight leading-none text-gray-900 dark:text-gray-100"
        >
          Audio Analysis
        </h1>
        <p class="text-sm text-gray-600 dark:text-gray-400 mt-1.5">
          Monitoring for the audio analysis enrichment worker
        </p>
      </div>
      <div class="flex items-center gap-3">
        <span class="text-xs text-gray-500 dark:text-gray-500">
          Updated {{ timeOnly.format(lastUpdated) }}
        </span>
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          :loading="isRefreshing"
          icon="i-mdi-refresh"
          @click="handleRefresh()"
        >
          Refresh
        </UButton>
      </div>
    </header>

    <UAlert
      v-if="hasError"
      color="error"
      variant="soft"
      title="Could not reach the monitoring data"
      description="Some sections may be stale or empty. The server may be offline."
      icon="i-mdi-connection"
    />

    <section class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div
        v-for="tile in kpiTiles"
        :key="tile.key"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-5 py-4 flex flex-col gap-2"
      >
        <div class="flex items-center gap-1.5">
          <Icon
            :icon="tile.icon"
            class="w-3.5 h-3.5 shrink-0"
            :class="tile.tone === 'warning' ? 'text-amber-500' : 'text-gray-400 dark:text-gray-500'"
          />
          <span class="text-xs font-medium text-gray-600 dark:text-gray-400 truncate">
            {{ tile.label }}
          </span>
        </div>
        <span
          class="text-2xl font-semibold tabular-nums"
          :class="
            tile.tone === 'warning'
              ? 'text-amber-600 dark:text-amber-400'
              : 'text-gray-900 dark:text-gray-100'
          "
        >
          {{ tile.value }}
        </span>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:waveform" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Queue Depth</h2>
        <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          {{ queueTotal }} file{{ queueTotal === 1 ? "" : "s" }} in flight
        </span>
      </div>

      <div v-if="queue.status.value === 'pending'" class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div
          v-for="i in 2"
          :key="i"
          class="h-32 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
        />
      </div>

      <div v-else-if="queueByBackbone.length" class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div
          v-for="group in queueByBackbone"
          :key="group.backbone"
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm overflow-hidden transition-shadow duration-200 hover:shadow-sm"
        >
          <div
            class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2"
          >
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :style="{ background: backboneColor(group.backbone) }"
            />
            <span class="text-sm font-semibold text-gray-800 dark:text-gray-100">
              {{ group.backbone }}
            </span>
            <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
              {{ group.rows.reduce((sum, row) => sum + row.count, 0) }} queued
            </span>
          </div>
          <div class="divide-y divide-gray-200/60 dark:divide-gray-700/60">
            <div
              v-for="row in group.rows"
              :key="row.status"
              class="px-5 py-2.5 flex items-center gap-3"
            >
              <span
                class="w-2 h-2 rounded-full shrink-0"
                :class="fileStatusMeta[row.status]?.dot"
              />
              <span class="text-sm text-gray-600 dark:text-gray-400">
                {{ fileStatusMeta[row.status]?.label ?? row.status }}
              </span>
              <span
                class="ml-auto text-sm font-semibold text-gray-800 dark:text-gray-100 tabular-nums"
              >
                {{ row.count }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <div
        v-else
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:waveform" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No batches in flight</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Files currently being analyzed will appear here as they are dispatched to the worker.
          </p>
        </div>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:clock-alert-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Stuck Files</h2>
        <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          Pending past the server threshold
        </span>
      </div>

      <div
        v-if="stuckQuery.status.value === 'pending'"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-5 space-y-3 animate-pulse"
      >
        <div v-for="i in 3" :key="i" class="h-8 rounded-lg bg-black/5 dark:bg-white/5" />
      </div>

      <div
        v-else-if="stuckSorted.length"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-200/60 dark:divide-gray-700/60 overflow-hidden"
      >
        <div
          v-for="row in stuckSorted"
          :key="row.fileId"
          class="px-5 py-3 flex items-center gap-3 transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02]"
        >
          <Icon icon="mdi:alert-circle-outline" class="w-4 h-4 text-amber-500 shrink-0" />
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium text-gray-800 dark:text-gray-100 font-mono">
              {{ shortId(row.fileId) }}
            </p>
            <p class="text-xs text-gray-500 dark:text-gray-500">
              Batch {{ shortId(row.batchId) }}
              <span class="inline-flex items-center gap-1 ml-1">
                <span
                  class="w-1.5 h-1.5 rounded-full inline-block"
                  :style="{ background: backboneColor(row.backbone) }"
                />
                {{ row.backbone }}
              </span>
            </p>
          </div>
          <span class="text-xs text-gray-500 dark:text-gray-500 shrink-0">
            {{ formatDate(row.createdAt) }}
          </span>
        </div>
      </div>

      <div
        v-else
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:check-circle-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No stuck files</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            No files have been pending longer than the configured threshold.
          </p>
        </div>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2 flex-wrap">
        <Icon icon="mdi:chart-line" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Failure Rate</h2>
        <div class="ml-auto flex items-center gap-2">
          <div class="flex items-center gap-1 p-1 rounded-lg bg-black/5 dark:bg-white/5">
            <button
              v-for="opt in rangeOptions"
              :key="opt.label"
              class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
              :class="
                rangeMs === opt.value
                  ? 'bg-white dark:bg-white/10 text-gray-900 dark:text-gray-100 shadow-sm'
                  : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'
              "
              @click="rangeMs = opt.value"
            >
              {{ opt.label }}
            </button>
          </div>
          <div class="flex items-center gap-1 p-1 rounded-lg bg-black/5 dark:bg-white/5">
            <button
              v-for="opt in bucketOptions"
              :key="opt.value"
              class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
              :class="
                bucket === opt.value
                  ? 'bg-white dark:bg-white/10 text-gray-900 dark:text-gray-100 shadow-sm'
                  : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'
              "
              @click="bucket = opt.value"
            >
              {{ opt.label }}
            </button>
          </div>
        </div>
      </div>

      <div
        v-if="failure.status.value === 'pending'"
        class="h-72 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
      />

      <div
        v-else-if="failureEmpty"
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:chart-line" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
            No data for this period
          </p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Completed analyses in the selected range will show failure rates per backbone.
          </p>
        </div>
      </div>

      <div
        v-else
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4 h-72"
      >
        <Line :data="failureChartData" :options="failureChartOptions" />
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:timer-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Durations</h2>
        <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          Completed at minus created at
        </span>
      </div>

      <div
        v-if="durationQuery.status.value === 'pending'"
        class="grid grid-cols-1 md:grid-cols-2 gap-4"
      >
        <div
          v-for="i in 2"
          :key="i"
          class="h-28 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
        />
      </div>

      <div
        v-else-if="(durationQuery.data.value ?? []).length"
        class="grid grid-cols-1 md:grid-cols-2 gap-4"
      >
        <div
          v-for="row in durationQuery.data.value"
          :key="row.backbone"
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-5 py-4 transition-shadow duration-200 hover:shadow-sm"
        >
          <div class="flex items-center gap-2 mb-3">
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :style="{ background: backboneColor(row.backbone) }"
            />
            <span class="text-sm font-semibold text-gray-800 dark:text-gray-100">
              {{ row.backbone }}
            </span>
          </div>
          <div class="grid grid-cols-3 divide-x divide-gray-200/60 dark:divide-gray-700/60">
            <div class="pr-3">
              <p class="text-[11px] uppercase tracking-wider text-gray-500 dark:text-gray-400">
                Average
              </p>
              <p class="text-lg font-semibold text-gray-900 dark:text-gray-100 tabular-nums mt-0.5">
                {{ formatDuration(row.averageSeconds) }}
              </p>
            </div>
            <div class="px-3">
              <p class="text-[11px] uppercase tracking-wider text-gray-500 dark:text-gray-400">
                Median
              </p>
              <p class="text-lg font-semibold text-gray-900 dark:text-gray-100 tabular-nums mt-0.5">
                {{ formatDuration(row.medianSeconds) }}
              </p>
            </div>
            <div class="pl-3">
              <p class="text-[11px] uppercase tracking-wider text-gray-500 dark:text-gray-400">
                Max
              </p>
              <p class="text-lg font-semibold text-gray-900 dark:text-gray-100 tabular-nums mt-0.5">
                {{ formatDuration(row.maxSeconds) }}
              </p>
            </div>
          </div>
        </div>
      </div>

      <div
        v-else
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:timer-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No durations yet</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Once analyses finish, average, median, and max processing times appear per backbone.
          </p>
        </div>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:chart-bar" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Volume by Hour</h2>
        <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          Analyses created per hour
        </span>
      </div>

      <div
        v-if="volume.status.value === 'pending'"
        class="h-60 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
      />

      <div
        v-else-if="volumeEmpty"
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:chart-bar" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
            No volume in this range
          </p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Hourly analysis volume for the selected period will appear here.
          </p>
        </div>
      </div>

      <div
        v-else
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4 h-60"
      >
        <Bar :data="volumeChartData" :options="volumeChartOptions" />
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:history" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Recent Batches</h2>
        <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">Latest 20</span>
      </div>

      <div
        v-if="recent.status.value === 'pending'"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-5 space-y-3 animate-pulse"
      >
        <div v-for="i in 5" :key="i" class="h-8 rounded-lg bg-black/5 dark:bg-white/5" />
      </div>

      <div
        v-else-if="(recent.data.value ?? []).length"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm overflow-hidden"
      >
        <div
          class="hidden md:grid px-5 py-2.5 border-b border-gray-200/70 dark:border-gray-700/70 grid-cols-[1fr_90px_110px_100px_100px] gap-4 items-center"
        >
          <span
            class="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500"
          >
            Backbone
          </span>
          <span
            class="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500"
          >
            Status
          </span>
          <span
            class="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500 text-right"
          >
            Progress
          </span>
          <span
            class="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500 text-right"
          >
            Created
          </span>
          <span
            class="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500 text-right"
          >
            Completed
          </span>
        </div>
        <!--
          One row template, not two. Named grid areas hold identity / status /
          progress / created / completed; only grid-template-areas and
          grid-template-columns change at md, so the browser just repositions
          five existing nodes instead of us shipping two full copies of the row.
        -->
        <div
          v-for="(batch, index) in recent.data.value"
          :key="batch.id"
          class="grid grid-cols-4 gap-x-3 gap-y-2 items-center px-4 py-3 [grid-template-areas:'identity_identity_status_status'_'progress_progress_progress_progress'_'created_created_completed_completed'] md:grid-cols-[1fr_90px_110px_100px_100px] md:gap-4 md:px-5 md:[grid-template-areas:'identity_status_progress_created_completed'] transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02]"
          :class="{ 'border-t border-gray-200/70 dark:border-gray-700/70': index > 0 }"
        >
          <div class="[grid-area:identity] min-w-0 flex items-center gap-2">
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :style="{ background: backboneColor(batch.backbone) }"
            />
            <div class="min-w-0">
              <p class="text-sm font-medium text-gray-800 dark:text-gray-100 font-mono truncate">
                {{ shortId(batch.id) }}
              </p>
              <p class="text-xs text-gray-500 dark:text-gray-500">{{ batch.backbone }}</p>
            </div>
          </div>

          <span
            class="[grid-area:status] justify-self-end md:justify-self-start text-xs font-semibold px-2.5 py-1 rounded-full"
            :class="[
              batchStatusMeta[batch.status]?.bg ?? 'bg-gray-500/10',
              batchStatusMeta[batch.status]?.text ?? 'text-gray-600 dark:text-gray-400',
            ]"
          >
            {{ batchStatusMeta[batch.status]?.label ?? batch.status }}
          </span>

          <div class="[grid-area:progress] flex items-center gap-2 md:justify-self-end">
            <div
              class="flex-1 md:flex-none md:w-12 h-1 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden"
            >
              <div
                class="h-full rounded-full bg-gray-500 dark:bg-gray-400"
                :style="{
                  width:
                    batch.filesTotal > 0
                      ? `${Math.round((batch.filesCompleted / batch.filesTotal) * 100)}%`
                      : '0%',
                }"
              />
            </div>
            <span class="text-xs text-gray-500 dark:text-gray-500 tabular-nums shrink-0">
              {{ batch.filesCompleted }} / {{ batch.filesTotal }}
            </span>
          </div>

          <span class="[grid-area:created] text-xs text-gray-500 dark:text-gray-500 md:text-right">
            <span class="md:hidden">Created </span>{{ formatDate(batch.createdAt) }}
          </span>

          <span class="[grid-area:completed] text-xs text-gray-500 dark:text-gray-500 text-right">
            <span class="md:hidden">Completed </span
            >{{ batch.completedAt ? formatDate(batch.completedAt) : "—" }}
          </span>
        </div>
      </div>

      <div
        v-else
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
      >
        <Icon icon="mdi:history" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No batches yet</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Batches dispatched to the audio analysis worker will appear here.
          </p>
        </div>
      </div>
    </section>

    <!-- Incidents (D12): locked to MediaMetadata, fed by the deep-link contract -->
    <section class="space-y-2">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:alert-circle-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">
          Audio Analysis incidents
        </h2>
      </div>
      <EventsFeed
        :date-range="feedDateRange"
        :initial-severity="initialSeverity"
        :highlight-id="highlightId"
        :initial-service-type="ServiceType.MediaMetadata"
        locked-service
      />
    </section>
  </div>
</template>
