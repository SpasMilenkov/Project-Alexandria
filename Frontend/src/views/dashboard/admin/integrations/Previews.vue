<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import {
  BarElement,
  CategoryScale,
  type ChartData,
  Chart as ChartJS,
  type ChartOptions,
  Filler,
  Legend,
  LineElement,
  LinearScale,
  PointElement,
  Title,
  Tooltip,
} from "chart.js";
import { computed, ref, watch } from "vue";
import { Bar, Line } from "vue-chartjs";
import { useRoute, useRouter } from "vue-router";

import type { PreviewJobType, PreviewStatsBucket } from "@/api/previewsStats";

import { PREVIEW_JOB_STATUS, PREVIEW_JOB_TYPE, PREVIEW_STATS_BUCKET } from "@/api/previewsStats";
import EventsFeed from "@/components/dashboard/admin/monitoring/EventsFeed.vue";
import { useTheme } from "@/composables/useTheme";
import { ServiceType } from "@/enums";
import {
  previewsJobOverview,
  previewsJobTrend,
  previewsOverview,
  previewsVolume,
} from "@/queries/previewsStats";
import { parseDeepLinkQuery } from "@/utils/serviceDashboardRouting";
import { formatBytes } from "@/utils/size.utils";

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

const route = useRoute();
const router = useRouter();
const { isDark } = useTheme();

type Scope = "all" | "media" | "docs";

// ?service= picks the half (deep links); absent → both
const scope = ref<Scope>("all");

const feedDateRange = ref<{ from: Date; to: Date } | null>(null);
const initialSeverity = ref<number | null>(null);
const highlightId = ref<string | null>(null);

const applyQuery = () => {
  const params = parseDeepLinkQuery(route.query);

  if (params.service === ServiceType.MediaPreviews) scope.value = "media";
  else if (params.service === ServiceType.DocumentPreviews) scope.value = "docs";
  else scope.value = "all";

  feedDateRange.value = params.from && params.to ? { from: params.from, to: params.to } : null;
  initialSeverity.value = params.severity ?? null;
  highlightId.value = params.eventId ?? null;
};

watch(() => route.query, applyQuery, { immediate: true });

const setScope = (next: Scope) => {
  scope.value = next;
  const query = { ...route.query };
  if (next === "all") delete query.service;
  else {
    query.service = String(
      next === "media" ? ServiceType.MediaPreviews : ServiceType.DocumentPreviews,
    );
  }
  void router.replace({ query });
};

const SCOPE_TABS: { label: string; value: Scope }[] = [
  { label: "Both", value: "all" },
  { label: "Media Previews", value: "media" },
  { label: "Document Previews", value: "docs" },
];

// Overview + volume
const rangeOptions = [
  { label: "24h", value: 24 * 60 * 60 * 1000 },
  { label: "7d", value: 7 * 24 * 60 * 60 * 1000 },
] as const;

const bucketOptions: { label: string; value: PreviewStatsBucket }[] = [
  { label: "Hourly", value: PREVIEW_STATS_BUCKET.Hour },
  { label: "Daily", value: PREVIEW_STATS_BUCKET.Day },
];

const rangeMs = ref(rangeOptions[0].value);
const bucket = ref<PreviewStatsBucket>(PREVIEW_STATS_BUCKET.Hour);

const volumeParams = computed(() => {
  const to = Date.now();
  return {
    from: new Date(to - rangeMs.value).toISOString(),
    to: new Date(to).toISOString(),
    bucket: bucket.value,
  };
});

const { data: overview, isLoading: overviewLoading } = useQuery(previewsOverview());
const { data: volume, isLoading: volumeLoading } = useQuery(
  previewsVolume,
  () => volumeParams.value,
);

// Discrete preview jobs (P6): the active scope selects the backing job type,
// absent scope combines both preview worker types.
const scopeJobType = computed<PreviewJobType | undefined>(() => {
  if (scope.value === "media") return PREVIEW_JOB_TYPE.MediaPreview;
  if (scope.value === "docs") return PREVIEW_JOB_TYPE.DocumentPreview;
  return undefined;
});

const jobTrendParams = computed(() => ({ ...volumeParams.value, type: scopeJobType.value }));

const { data: jobOverview, isLoading: jobOverviewLoading } = useQuery(() =>
  previewsJobOverview(scopeJobType.value),
);
const { data: jobTrend, isLoading: jobTrendLoading } = useQuery(
  previewsJobTrend,
  () => jobTrendParams.value,
);

const jobStatusCount = (status: number): number =>
  jobOverview.value?.statusCounts.find((entry) => entry.status === status)?.count ?? 0;

const queueDepth = computed(
  () =>
    jobStatusCount(PREVIEW_JOB_STATUS.Queued) +
    jobStatusCount(PREVIEW_JOB_STATUS.Processing) +
    jobStatusCount(PREVIEW_JOB_STATUS.CancellationRequested),
);

const failedAllTime = computed(() => jobStatusCount(PREVIEW_JOB_STATUS.Failed));

const windowSuccessRate = computed(() => {
  const points = jobTrend.value?.failureRate ?? [];
  const total = points.reduce((sum, p) => sum + p.total, 0);
  const failed = points.reduce((sum, p) => sum + p.failed, 0);
  if (total === 0) return null;
  return Math.round((1 - failed / total) * 1000) / 10;
});

const kindTotal = (kind: number): number =>
  overview.value?.byKind.find((entry) => entry.kind === kind)?.count ?? 0;

const totalArtifacts = computed(() =>
  (overview.value?.byKind ?? []).reduce((sum, entry) => sum + entry.count, 0),
);

const totalSizeBytes = computed(() =>
  (overview.value?.byKind ?? []).reduce((sum, entry) => sum + entry.totalSizeBytes, 0),
);

const gridColor = computed(() =>
  isDark.value ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)",
);
const tickColor = computed(() => (isDark.value ? "#9ca3af" : "#6b7280"));

const bucketLabel = (iso: string): string => {
  const date = new Date(iso);
  return bucket.value === PREVIEW_STATS_BUCKET.Hour
    ? date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
};

const volumeChartData = computed<ChartData<"bar">>(() => ({
  labels: (volume.value?.points ?? []).map((p) => bucketLabel(p.bucketStart)),
  datasets: [
    {
      label: "Thumbnails",
      data: (volume.value?.points ?? []).map((p) => p.thumbnails),
      backgroundColor: "#5B7FA6",
      borderRadius: 3,
    },
    {
      label: "Preview images",
      data: (volume.value?.points ?? []).map((p) => p.previews),
      backgroundColor: "#C17B5C",
      borderRadius: 3,
    },
  ],
}));

const volumeOptions = computed<ChartOptions<"bar">>(() => ({
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

const rateChartData = computed<ChartData<"line">>(() => ({
  labels: (jobTrend.value?.failureRate ?? []).map((p) => bucketLabel(p.bucketStart)),
  datasets: [
    {
      label: "Failure rate %",
      data: (jobTrend.value?.failureRate ?? []).map((p) => p.failureRate),
      borderColor: "#dc2626",
      backgroundColor: "rgba(220, 38, 38, 0.15)",
      tension: 0.3,
      fill: true,
      pointRadius: 2,
    },
  ],
}));

const rateOptions = computed<ChartOptions<"line">>(() => ({
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
      max: 100,
      grid: { color: gridColor.value },
      ticks: { color: tickColor.value },
    },
  },
}));

interface IncidentScope {
  label: string;
  service: ServiceType;
}

const incidentScopes = computed<IncidentScope[]>(() => {
  if (scope.value === "media") {
    return [{ label: "Media Previews", service: ServiceType.MediaPreviews }];
  }
  if (scope.value === "docs") {
    return [{ label: "Document Previews", service: ServiceType.DocumentPreviews }];
  }
  return [
    { label: "Media Previews", service: ServiceType.MediaPreviews },
    { label: "Document Previews", service: ServiceType.DocumentPreviews },
  ];
});
</script>

<template>
  <div class="p-3 sm:p-5 lg:p-6">
    <div class="max-w-400 mx-auto space-y-3 lg:space-y-4">
      <!-- Page header -->
      <div class="flex items-center justify-between gap-3 flex-wrap mb-1">
        <div>
          <h1 class="text-lg font-semibold text-gray-800 dark:text-gray-100">Previews Dashboard</h1>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
            Artifact throughput for the media and document preview workers
          </p>
        </div>
        <div class="flex items-center gap-1">
          <UButton
            v-for="tab in SCOPE_TABS"
            :key="tab.value"
            size="xs"
            :variant="scope === tab.value ? 'solid' : 'outline'"
            :color="scope === tab.value ? 'primary' : 'neutral'"
            @click="setScope(tab.value)"
          >
            {{ tab.label }}
          </UButton>
        </div>
      </div>

      <!-- Stat cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Total artifacts
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ totalArtifacts.toLocaleString() }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Total size
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-20 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ formatBytes(totalSizeBytes) }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Thumbnails
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ kindTotal(0).toLocaleString() }}
          </p>
        </div>
        <div
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface px-4 py-3"
        >
          <p class="text-[11px] uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Preview images
          </p>
          <USkeleton v-if="overviewLoading" class="h-7 w-12 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ kindTotal(1).toLocaleString() }}
          </p>
        </div>
      </div>

      <!-- Volume chart -->
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
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface p-4"
        >
          <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">
            Artifacts created
          </p>
          <!-- Bounded height is mandatory with maintainAspectRatio:false -->
          <div v-if="volumeLoading" class="h-56">
            <USkeleton class="h-full w-full rounded-xl" />
          </div>
          <div v-else class="relative h-56">
            <Bar :data="volumeChartData" :options="volumeOptions" />
          </div>
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
          <USkeleton v-if="jobOverviewLoading" class="h-7 w-12 mt-1" />
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
          <USkeleton v-if="jobOverviewLoading" class="h-7 w-12 mt-1" />
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
          <USkeleton v-if="jobTrendLoading" class="h-7 w-16 mt-1" />
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
          <USkeleton v-if="jobOverviewLoading" class="h-7 w-20 mt-1" />
          <p v-else class="text-2xl font-bold tabular-nums text-gray-900 dark:text-gray-100">
            {{ jobOverview?.duration ? `${Math.round(jobOverview.duration.avgMinutes)} min` : "—" }}
          </p>
        </div>
      </div>

      <!-- Failure rate chart -->
      <div class="space-y-3">
        <div
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-4"
        >
          <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Failure rate %</p>
          <!-- Bounded height is mandatory with maintainAspectRatio:false -->
          <div v-if="jobTrendLoading" class="h-56">
            <USkeleton class="h-full w-full rounded-xl" />
          </div>
          <div v-else class="relative h-56">
            <Line :data="rateChartData" :options="rateOptions" />
          </div>
        </div>
      </div>

      <!-- Incidents per scope -->
      <div v-for="incident in incidentScopes" :key="incident.service" class="space-y-2">
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300 px-1">
          {{ incident.label }} incidents
        </h2>
        <EventsFeed
          :date-range="feedDateRange"
          :initial-service-type="incident.service"
          :initial-severity="initialSeverity"
          :highlight-id="highlightId"
          locked-service
        />
      </div>
    </div>
  </div>
</template>
