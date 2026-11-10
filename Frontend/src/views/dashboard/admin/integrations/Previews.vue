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
  previewsJobOverview,
  previewsJobTrend,
  previewsOverview,
  previewsVolume,
} from "@/queries/previewsStats";
import { formatBucketLabel } from "@/utils/date-formatters";
import { formatBytes } from "@/utils/size";

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
  { label: "Media", value: "media" },
  { label: "Documents", value: "docs" },
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

// Minute ticks move the trailing window forward, which refetches the trend under a new key.
const { now } = useRollingNow();

const volumeParams = computed(() => {
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
} = useQuery(previewsOverview());

// Overview counts have a fixed key, so they are polled explicitly.
useLiveRefresh(refetchOverview, 60_000);
const { data: volume, isPending: volumeLoading } = useQuery(() =>
  previewsVolume(volumeParams.value),
);

// Discrete preview jobs (P6): the active scope selects the backing job type,
// absent scope combines both preview worker types.
const scopeJobType = computed<PreviewJobType | undefined>(() => {
  if (scope.value === "media") return PREVIEW_JOB_TYPE.MediaPreview;
  if (scope.value === "docs") return PREVIEW_JOB_TYPE.DocumentPreview;
  return undefined;
});

const jobTrendParams = computed(() => ({ ...volumeParams.value, type: scopeJobType.value }));

const {
  data: jobOverview,
  isPending: jobOverviewLoading,
  refetch: refetchJobOverview,
} = useQuery(() => previewsJobOverview(scopeJobType.value));

useLiveRefresh(refetchJobOverview, 60_000);
const { data: jobTrend, isPending: jobTrendLoading } = useQuery(() =>
  previewsJobTrend(jobTrendParams.value),
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

const bucketLabel = (iso: string): string =>
  formatBucketLabel(iso, bucket.value === PREVIEW_STATS_BUCKET.Hour ? "hour" : "day");

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
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <IntegrationPageHeader
      title="Previews"
      icon="mdi:image-multiple-outline"
      :services="[ServiceType.MediaPreviews, ServiceType.DocumentPreviews]"
    >
      <template #actions>
        <SegmentedControl
          :model-value="scope"
          :options="SCOPE_TABS"
          label="Preview worker"
          @update:model-value="setScope"
        />
        <SegmentedControl v-model="rangeMs" :options="rangeOptions" label="Time range" />
        <SegmentedControl v-model="bucket" :options="bucketOptions" label="Bucket size" />
      </template>
    </IntegrationPageHeader>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="flex flex-col gap-3">
        <section aria-labelledby="previews-artifacts" class="flex flex-col gap-3">
          <h2
            id="previews-artifacts"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            Artifacts
          </h2>

          <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="Total artifacts" :loading="overviewLoading">
              {{ totalArtifacts.toLocaleString() }}
            </StatTile>
            <StatTile label="Total size" :loading="overviewLoading">
              {{ formatBytes(totalSizeBytes) }}
            </StatTile>
            <StatTile label="Thumbnails" :loading="overviewLoading">
              {{ kindTotal(0).toLocaleString() }}
            </StatTile>
            <StatTile label="Preview images" :loading="overviewLoading">
              {{ kindTotal(1).toLocaleString() }}
            </StatTile>
          </div>

          <IntegrationPanel title="Artifacts created">
            <ChartFrame :loading="volumeLoading">
              <Bar :data="volumeChartData" :options="volumeOptions" />
            </ChartFrame>
          </IntegrationPanel>
        </section>

        <section aria-labelledby="previews-jobs" class="flex flex-col gap-3">
          <h2
            id="previews-jobs"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            Jobs
          </h2>

          <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="Queue depth" :loading="jobOverviewLoading">{{ queueDepth }}</StatTile>
            <StatTile
              label="Failed jobs"
              :loading="jobOverviewLoading"
              :tone="failedAllTime > 0 ? 'danger' : 'default'"
            >
              {{ failedAllTime }}
            </StatTile>
            <StatTile label="Success rate (window)" :loading="jobTrendLoading">
              {{ windowSuccessRate === null ? "—" : `${windowSuccessRate}%` }}
            </StatTile>
            <StatTile label="Avg duration (30d)" :loading="jobOverviewLoading">
              {{
                jobOverview?.duration ? `${Math.round(jobOverview.duration.avgMinutes)} min` : "—"
              }}
            </StatTile>
          </div>

          <IntegrationPanel title="Failure rate">
            <ChartFrame :loading="jobTrendLoading">
              <Line :data="rateChartData" :options="rateOptions" />
            </ChartFrame>
          </IntegrationPanel>

          <div class="grid grid-cols-1 gap-3 lg:grid-cols-2">
            <JobRecoveryPanel :type="JobType.MediaPreview" title="Media preview jobs" />
            <JobRecoveryPanel :type="JobType.DocumentPreview" title="Document preview jobs" />
          </div>
        </section>

        <section
          v-for="incident in incidentScopes"
          :key="incident.service"
          :aria-labelledby="`previews-incidents-${incident.service}`"
          class="flex flex-col gap-2"
        >
          <h2
            :id="`previews-incidents-${incident.service}`"
            class="px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
          >
            {{ incident.label }} incidents
          </h2>
          <EventsFeed
            :date-range="feedDateRange"
            :initial-service-type="incident.service"
            :initial-severity="initialSeverity"
            :highlight-id="highlightId"
            locked-service
          />
        </section>
      </div>
    </div>
  </div>
</template>
