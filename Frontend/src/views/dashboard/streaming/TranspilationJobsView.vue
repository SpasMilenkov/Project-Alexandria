<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { getLocalTimeZone, today } from "@internationalized/date";
import { useQuery } from "@pinia/colada";
import { computed, reactive, ref, watch } from "vue";

import type { TranspilationJobQuery, TranspilationJobResponse } from "@/api/streaming";

import { AudioRung, VideoRung } from "@/api/policy";
import { useAppToast } from "@/composables/useAppToast";
import { JobStatus } from "@/enums/job-status";
import { stopTranspilationJob } from "@/mutations/streaming";
import { getTranspilationJobs } from "@/queries/streaming";

const toast = useAppToast();

const currentPage = ref(1);
const pageSize = ref(50);
const statusFilter = ref<JobStatus | undefined>(undefined);
const isVideoFilter = ref<boolean | undefined>(undefined);

const filtersOpen = ref(false);

// Structural day value shared with the calendar components. The nominal
// CalendarDate class carries a private brand, so annotate structurally.
interface DayValue {
  day: number;
  month: number;
  year: number;
}

const createdFrom = ref<DayValue | null>(null);
const createdTo = ref<DayValue | null>(null);
const completedFrom = ref<DayValue | null>(null);
const completedTo = ref<DayValue | null>(null);
const minRetries = ref(0);

const isFiltered = computed(
  () =>
    statusFilter.value !== undefined ||
    isVideoFilter.value !== undefined ||
    createdFrom.value !== null ||
    createdTo.value !== null ||
    completedFrom.value !== null ||
    completedTo.value !== null ||
    minRetries.value > 0,
);

const startIso = (value: DayValue | null): string | undefined => {
  if (!value) return undefined;
  return new Date(Date.UTC(value.year, value.month - 1, value.day, 0, 0, 0)).toISOString();
};

const endIso = (value: DayValue | null): string | undefined => {
  if (!value) return undefined;
  return new Date(Date.UTC(value.year, value.month - 1, value.day, 23, 59, 59, 999)).toISOString();
};

const dayTime = (value: DayValue) => Date.UTC(value.year, value.month - 1, value.day);

const createdRangeValid = computed(() => {
  if (!createdFrom.value || !createdTo.value) return true;
  return dayTime(createdFrom.value) <= dayTime(createdTo.value);
});

const completedRangeValid = computed(() => {
  if (!completedFrom.value || !completedTo.value) return true;
  return dayTime(completedFrom.value) <= dayTime(completedTo.value);
});

const advancedParams = computed(() => ({
  createdAfter: createdRangeValid.value ? startIso(createdFrom.value) : undefined,
  createdBefore: createdRangeValid.value ? endIso(createdTo.value) : undefined,
  completedAfter: completedRangeValid.value ? startIso(completedFrom.value) : undefined,
  completedBefore: completedRangeValid.value ? endIso(completedTo.value) : undefined,
  minRetryCount: minRetries.value > 0 ? minRetries.value : undefined,
}));

const query = computed<TranspilationJobQuery>(() => ({
  currentPage: currentPage.value,
  pageSize: pageSize.value,
  status: statusFilter.value,
  isVideo: isVideoFilter.value,
  ...advancedParams.value,
}));

const { data, isLoading, error, refetch } = useQuery(() => getTranspilationJobs(query.value));
const { mutateAsync: updateJobStatus } = stopTranspilationJob();

const COUNT_STATUSES = [
  JobStatus.Queued,
  JobStatus.Partial,
  JobStatus.Processing,
  JobStatus.Ready,
  JobStatus.Failed,
  JobStatus.Cancelled,
];

const allCountQuery = useQuery(() =>
  getTranspilationJobs({
    currentPage: 1,
    pageSize: 1,
    isVideo: isVideoFilter.value,
    ...advancedParams.value,
  }),
);

const statusCountQueries = COUNT_STATUSES.map((status) =>
  useQuery(() =>
    getTranspilationJobs({
      currentPage: 1,
      pageSize: 1,
      status,
      isVideo: isVideoFilter.value,
      ...advancedParams.value,
    }),
  ),
);

const allCount = computed(() => allCountQuery.data.value?.totalCount);
const statusCounts = computed(() => {
  const out = new Map<JobStatus, number>();
  COUNT_STATUSES.forEach((status, index) => {
    const total = statusCountQueries[index].data.value?.totalCount;
    if (total !== undefined) out.set(status, total);
  });
  return out;
});
const failedCount = computed(() => statusCounts.value.get(JobStatus.Failed) ?? 0);

const isRefreshing = ref(false);

const handleRefresh = async () => {
  isRefreshing.value = true;
  try {
    await refetch();
  } finally {
    isRefreshing.value = false;
  }
};

const selectStatus = (status: JobStatus | undefined) => {
  statusFilter.value = status;
  currentPage.value = 1;
};

const selectMedia = (isVideo: boolean | undefined) => {
  isVideoFilter.value = isVideo;
  currentPage.value = 1;
};

const clearFilters = () => {
  statusFilter.value = undefined;
  isVideoFilter.value = undefined;
  resetAdvanced();
  currentPage.value = 1;
};

const resetAdvanced = () => {
  createdFrom.value = null;
  createdTo.value = null;
  completedFrom.value = null;
  completedTo.value = null;
  minRetries.value = 0;
};

const advancedFilterCount = computed(() => {
  let count = 0;
  if (createdFrom.value) count++;
  if (createdTo.value) count++;
  if (completedFrom.value) count++;
  if (completedTo.value) count++;
  if (minRetries.value > 0) count++;
  return count;
});

interface ActiveChip {
  key: string;
  label: string;
}

const formatShortDay = (value: DayValue) =>
  new Date(Date.UTC(value.year, value.month - 1, value.day)).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });

const activeChips = computed<ActiveChip[]>(() => {
  const chips: ActiveChip[] = [];
  if (createdFrom.value)
    chips.push({ key: "ca", label: `Created from ${formatShortDay(createdFrom.value)}` });
  if (createdTo.value)
    chips.push({ key: "cb", label: `Created until ${formatShortDay(createdTo.value)}` });
  if (completedFrom.value)
    chips.push({ key: "da", label: `Completed from ${formatShortDay(completedFrom.value)}` });
  if (completedTo.value)
    chips.push({ key: "db", label: `Completed until ${formatShortDay(completedTo.value)}` });
  if (minRetries.value > 0) chips.push({ key: "rt", label: `${minRetries.value}+ retries` });
  return chips;
});

const removeChip = (key: string) => {
  if (key === "ca") createdFrom.value = null;
  else if (key === "cb") createdTo.value = null;
  else if (key === "da") completedFrom.value = null;
  else if (key === "db") completedTo.value = null;
  else minRetries.value = 0;
};

const applyPreset = (days: number) => {
  createdFrom.value = today(getLocalTimeZone()).subtract({ days });
  createdTo.value = null;
};

const jobs = computed(() => data.value?.items ?? []);

const hasLive = computed(() => jobs.value.some((job) => job.status === JobStatus.Processing));

interface JobGroup {
  versionId: string;
  fileName?: string;
  versionNumber?: number;
  isVideo: boolean;
  jobs: TranspilationJobResponse[];
}

const groups = computed<JobGroup[]>(() => {
  const map = new Map<string, TranspilationJobResponse[]>();
  for (const job of jobs.value) {
    const existing = map.get(job.versionId);
    if (existing) existing.push(job);
    else map.set(job.versionId, [job]);
  }
  return [...map.entries()].map(([versionId, items]) => ({
    versionId,
    fileName: items[0].fileName,
    versionNumber: items[0].versionNumber,
    isVideo: items[0].isVideo,
    jobs: items,
  }));
});

const ATTENTION = new Set<JobStatus>([
  JobStatus.Queued,
  JobStatus.Partial,
  JobStatus.Processing,
  JobStatus.Failed,
  JobStatus.CancellationRequested,
]);

const hasAttention = (group: JobGroup) => group.jobs.some((job) => ATTENTION.has(job.status));

const openOverrides = reactive<Record<string, boolean>>({});
const isGroupOpen = (group: JobGroup) => openOverrides[group.versionId] ?? hasAttention(group);
const toggleGroup = (group: JobGroup) => {
  openOverrides[group.versionId] = !isGroupOpen(group);
};

const groupSummary = (group: JobGroup) => {
  const counts = new Map<JobStatus, number>();
  for (const job of group.jobs) counts.set(job.status, (counts.get(job.status) ?? 0) + 1);
  return [...counts.entries()].map(([status, count]) => ({ status, count }));
};

const expandedError = reactive<Record<string, boolean>>({});
const toggleError = (jobId: string) => {
  expandedError[jobId] = !expandedError[jobId];
};

const copyError = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text);
    toast.success("Error copied to clipboard");
  } catch {
    toast.error("Failed to copy");
  }
};

const actionInProgress = ref<string | null>(null);
const requeuePopoverOpen = reactive<Record<string, boolean>>({});
const pendingAudioRungs = reactive<Record<string, AudioRung[]>>({});
const pendingVideoRungs = reactive<Record<string, VideoRung[]>>({});

const initRequeueRungs = (job: TranspilationJobResponse) => {
  pendingAudioRungs[job.id] = [...(job.audioRungs ?? [])];
  pendingVideoRungs[job.id] = [...(job.videoRungs ?? [])];
};

watch(jobs, (next) => {
  const live = new Set(next.map((job) => job.id));
  for (const id of Object.keys(pendingAudioRungs)) {
    if (!live.has(id)) delete pendingAudioRungs[id];
  }
  for (const id of Object.keys(pendingVideoRungs)) {
    if (!live.has(id)) delete pendingVideoRungs[id];
  }
  for (const id of Object.keys(requeuePopoverOpen)) {
    if (!live.has(id)) delete requeuePopoverOpen[id];
  }
  for (const id of Object.keys(expandedError)) {
    if (!live.has(id)) delete expandedError[id];
  }
  for (const id of Object.keys(openOverrides)) {
    if (!live.has(id)) delete openOverrides[id];
  }
});

watch(
  [statusFilter, isVideoFilter, createdFrom, createdTo, completedFrom, completedTo, minRetries],
  () => {
    for (const id of Object.keys(openOverrides)) delete openOverrides[id];
    currentPage.value = 1;
  },
);

const toggleAudioRung = (jobId: string, rung: AudioRung) => {
  const arr = pendingAudioRungs[jobId] ?? [];
  const idx = arr.indexOf(rung);
  if (idx === -1) arr.push(rung);
  else arr.splice(idx, 1);
  pendingAudioRungs[jobId] = arr;
};

const toggleVideoRung = (jobId: string, rung: VideoRung) => {
  const arr = pendingVideoRungs[jobId] ?? [];
  const idx = arr.indexOf(rung);
  if (idx === -1) arr.push(rung);
  else arr.splice(idx, 1);
  pendingVideoRungs[jobId] = arr;
};

const audioRungOptions = [
  { label: "96k", value: AudioRung.Kbps96 },
  { label: "128k", value: AudioRung.Kbps128 },
  { label: "192k", value: AudioRung.Kbps192 },
  { label: "256k", value: AudioRung.Kbps256 },
  { label: "320k", value: AudioRung.Kbps320 },
];

const videoRungOptions = [
  { label: "360p", value: VideoRung.P360 },
  { label: "480p", value: VideoRung.P480 },
  { label: "720p", value: VideoRung.P720 },
  { label: "1080p", value: VideoRung.P1080 },
  { label: "1440p", value: VideoRung.P1440 },
  { label: "2160p", value: VideoRung.P2160 },
];

const handleCancel = async (job: TranspilationJobResponse) => {
  actionInProgress.value = job.id;
  const targetStatus =
    job.status === JobStatus.Processing ? JobStatus.CancellationRequested : JobStatus.Cancelled;
  try {
    await updateJobStatus({ jobId: job.id, status: targetStatus });
    await refetch();
    toast.info(
      targetStatus === JobStatus.CancellationRequested ? "Cancellation requested" : "Job cancelled",
    );
  } catch (err) {
    toast.error("Failed to cancel job", err);
  } finally {
    actionInProgress.value = null;
  }
};

const handleRequeue = async (job: TranspilationJobResponse) => {
  actionInProgress.value = job.id;
  try {
    await updateJobStatus({
      jobId: job.id,
      status: JobStatus.Queued,
      audioRungs: job.isVideo ? undefined : pendingAudioRungs[job.id],
      videoRungs: job.isVideo ? pendingVideoRungs[job.id] : undefined,
    });
    requeuePopoverOpen[job.id] = false;
    await refetch();
    toast.success("Job requeued");
  } catch (err) {
    toast.error("Failed to requeue job", err);
  } finally {
    actionInProgress.value = null;
  }
};

interface StatusConfig {
  label: string;
  icon: string;
  chipClass: string;
  barClass: string;
}

const statusConfig: Record<JobStatus, StatusConfig> = {
  [JobStatus.Queued]: {
    label: "Queued",
    icon: "mdi:clock-outline",
    chipClass: "bg-gray-200/80 dark:bg-white/10 text-gray-500 dark:text-white/50",
    barClass: "bg-gray-400 dark:bg-white/30",
  },
  [JobStatus.Partial]: {
    label: "Partial",
    icon: "mdi:clock-alert-outline",
    chipClass: "bg-amber-100 dark:bg-amber-500/15 text-amber-700 dark:text-amber-400",
    barClass: "bg-amber-400 dark:bg-amber-500",
  },
  [JobStatus.Processing]: {
    label: "Processing",
    icon: "mdi:loading",
    chipClass: "bg-blue-100 dark:bg-blue-500/15 text-blue-700 dark:text-blue-400",
    barClass: "bg-primary",
  },
  [JobStatus.CancellationRequested]: {
    label: "Cancelling",
    icon: "mdi:loading",
    chipClass: "bg-orange-100 dark:bg-orange-500/15 text-orange-600 dark:text-orange-400",
    barClass: "bg-orange-400 dark:bg-orange-500",
  },
  [JobStatus.Cancelled]: {
    label: "Cancelled",
    icon: "mdi:close-circle",
    chipClass: "bg-gray-200/80 dark:bg-white/[0.07] text-gray-400 dark:text-white/35",
    barClass: "bg-gray-300 dark:bg-white/20",
  },
  [JobStatus.Ready]: {
    label: "Ready",
    icon: "mdi:check-circle-outline",
    chipClass: "bg-emerald-100 dark:bg-emerald-500/15 text-emerald-700 dark:text-emerald-400",
    barClass: "bg-emerald-400 dark:bg-emerald-500",
  },
  [JobStatus.Failed]: {
    label: "Failed",
    icon: "mdi:alert-circle-outline",
    chipClass: "bg-red-100 dark:bg-red-500/15 text-red-600 dark:text-red-400",
    barClass: "bg-red-400 dark:bg-red-500",
  },
};

const canCancel = (status: JobStatus) =>
  status === JobStatus.Queued || status === JobStatus.Processing || status === JobStatus.Partial;

const canRequeue = (status: JobStatus) =>
  status === JobStatus.Failed || status === JobStatus.Cancelled || status === JobStatus.Ready;

const isSpinning = (status: JobStatus) =>
  status === JobStatus.Processing || status === JobStatus.CancellationRequested;

const showProgress = (status: JobStatus) =>
  status !== JobStatus.Ready && status !== JobStatus.Cancelled;

const VIDEO_RUNG_LABELS: Record<number, string> = {
  0: "360p",
  1: "480p",
  2: "720p",
  3: "1080p",
  4: "1440p",
  5: "2160p",
};

const AUDIO_RUNG_LABELS: Record<number, string> = {
  0: "96k",
  1: "128k",
  2: "192k",
  3: "256k",
  4: "320k",
};

const rungChips = (job: TranspilationJobResponse): string[] => {
  if (job.isVideo)
    return (job.videoRungs ?? []).map((rung) => VIDEO_RUNG_LABELS[rung] ?? `${rung}`);
  return (job.audioRungs ?? []).map((rung) => AUDIO_RUNG_LABELS[rung] ?? `${rung}`);
};

const formatDate = (iso?: string) =>
  iso
    ? new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
        new Date(iso),
      )
    : "—";

const formatRelative = (iso?: string) => {
  if (!iso) return "—";
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  if (days < 8) return `${days} d ago`;
  return formatDate(iso);
};

const statusFilterOptions = [
  { label: "All", value: undefined },
  { label: "Queued", value: JobStatus.Queued },
  { label: "Partial", value: JobStatus.Partial },
  { label: "Processing", value: JobStatus.Processing },
  { label: "Ready", value: JobStatus.Ready },
  { label: "Failed", value: JobStatus.Failed },
  { label: "Cancelled", value: JobStatus.Cancelled },
];

const mediaFilterOptions = [
  { label: "All", value: undefined, icon: "mdi:all-inclusive" },
  { label: "Video", value: true, icon: "lucide:video" },
  { label: "Audio", value: false, icon: "mdi:music-note-outline" },
];
</script>

<template>
  <div class="px-6 py-8">
    <!-- Page header -->
    <div class="flex items-center gap-3 mb-6 flex-wrap">
      <div>
        <h1 class="text-lg font-semibold text-gray-800 dark:text-white/90 m-0">
          Transpilation Jobs
        </h1>
        <p class="text-xs text-gray-400 dark:text-white/30 mt-0.5 m-0">
          {{ groups.length }} file{{ groups.length !== 1 ? "s" : "" }},
          {{ data?.totalCount ?? 0 }} job{{ (data?.totalCount ?? 0) !== 1 ? "s" : "" }}
        </p>
      </div>
      <span
        v-if="hasLive && !isLoading"
        class="inline-flex items-center gap-2 h-7 px-3 rounded-full bg-black/[0.04] dark:bg-white/[0.06] text-xs text-gray-600 dark:text-white/50"
      >
        <span class="w-2 h-2 rounded-full bg-primary animate-pulse" />
        Updating live
      </span>
      <span class="flex-1" />
      <button
        class="flex items-center justify-center w-7 h-7 rounded-lg text-gray-400 dark:text-white/35 hover:bg-black/[0.05] dark:hover:bg-white/[0.07] hover:text-gray-600 dark:hover:text-white/55 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        :disabled="isRefreshing || isLoading"
        :title="isRefreshing ? 'Refreshing…' : 'Refresh jobs'"
        @click="handleRefresh"
      >
        <Icon icon="mdi:refresh" class="w-4 h-4" :class="{ 'animate-spin': isRefreshing }" />
      </button>
    </div>

    <!-- Filters with live counts -->
    <div v-if="!error && (isLoading || groups.length > 0 || isFiltered)">
      <div class="filter-tabs" role="tablist" aria-label="Status">
        <button
          v-for="opt in statusFilterOptions"
          :key="String(opt.value)"
          role="tab"
          :aria-selected="statusFilter === opt.value"
          class="filter-tab"
          :class="{ 'filter-tab-on': statusFilter === opt.value }"
          @click="selectStatus(opt.value)"
        >
          {{ opt.label }}
          <em>{{
            opt.value === undefined ? (allCount ?? "") : (statusCounts.get(opt.value) ?? "")
          }}</em>
          <span
            v-if="statusFilter === opt.value"
            class="absolute left-0 right-0 bottom-0 h-0.5 rounded-full bg-primary"
          />
        </button>
      </div>

      <div class="filters-toolbar">
        <div
          class="flex items-center flex-wrap sm:flex-nowrap gap-1 filter-group"
          role="group"
          aria-label="Media type"
        >
          <button
            v-for="opt in mediaFilterOptions"
            :key="String(opt.value)"
            class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors whitespace-nowrap"
            :class="
              isVideoFilter === opt.value
                ? 'bg-primary text-white'
                : 'bg-black/[0.04] dark:bg-white/[0.06] text-gray-600 dark:text-white/50 hover:bg-black/[0.07] dark:hover:bg-white/[0.09]'
            "
            @click="selectMedia(opt.value)"
          >
            <Icon :icon="opt.icon" class="w-3.5 h-3.5" />
            {{ opt.label }}
          </button>
        </div>

        <button
          class="flex items-center gap-1.5 h-[38px] px-3 rounded-lg text-xs font-medium border transition-colors whitespace-nowrap shrink-0"
          :class="
            filtersOpen || advancedFilterCount > 0
              ? 'border-primary/60 bg-primary/10 text-primary'
              : 'border-black/[0.08] dark:border-white/[0.09] text-gray-600 dark:text-white/50 hover:bg-black/[0.04] dark:hover:bg-white/[0.06]'
          "
          :aria-expanded="filtersOpen"
          @click="filtersOpen = !filtersOpen"
        >
          <Icon icon="lucide:filter" class="w-3.5 h-3.5" />
          Filters
          <span
            v-if="advancedFilterCount > 0"
            class="min-w-[18px] h-[18px] px-1 rounded-full bg-primary text-white text-[11px] font-bold inline-grid place-items-center tabular-nums"
          >
            {{ advancedFilterCount }}
          </span>
        </button>

        <span
          v-for="chip in activeChips"
          :key="chip.key"
          class="inline-flex items-center gap-1 h-7 pl-3 pr-1 rounded-full bg-black/[0.04] dark:bg-white/[0.06] text-xs text-gray-700 dark:text-white/70 whitespace-nowrap"
        >
          {{ chip.label }}
          <button
            class="w-5 h-5 rounded-full grid place-items-center text-gray-400 dark:text-white/30 hover:text-gray-700 dark:hover:text-white/70 transition-colors"
            :aria-label="`Remove filter ${chip.label}`"
            @click="removeChip(chip.key)"
          >
            <Icon icon="lucide:x" class="w-3 h-3" />
          </button>
        </span>
        <button
          v-if="activeChips.length > 0"
          class="px-2.5 h-7 rounded-lg text-xs font-medium text-gray-500 dark:text-white/40 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-colors whitespace-nowrap"
          @click="resetAdvanced"
        >
          Clear all
        </button>
      </div>

      <!-- More filters panel -->
      <div
        v-if="filtersOpen"
        class="rounded-xl border border-black/[0.08] dark:border-white/[0.09] frosted-glass glass-surface p-4 sm:p-6 mb-4"
      >
        <div class="flex items-center gap-2 mb-6">
          <p class="text-sm font-semibold text-gray-700 dark:text-white/70 m-0">More filters</p>
          <span class="flex-1" />
          <button
            class="px-2.5 h-7 rounded-lg text-xs font-medium text-gray-500 dark:text-white/40 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            :disabled="advancedFilterCount === 0"
            @click="resetAdvanced"
          >
            Reset
          </button>
          <button
            class="flex items-center justify-center w-7 h-7 rounded-lg text-gray-400 dark:text-white/35 hover:bg-black/[0.05] dark:hover:bg-white/[0.07] transition-colors"
            aria-label="Close filters"
            @click="filtersOpen = false"
          >
            <Icon icon="lucide:x" class="w-4 h-4" />
          </button>
        </div>

        <div class="filter-grid">
          <div class="flex flex-col gap-3">
            <p class="text-[13px] font-semibold text-gray-700 dark:text-white/70 m-0">Created</p>
            <div class="grid grid-cols-2 gap-3">
              <UFormField name="createdFrom">
                <!-- @vue-ignore -->
                <UInputDate v-model="createdFrom" class="w-full">
                  <template #trailing>
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="link"
                        size="sm"
                        icon="i-lucide-calendar"
                        aria-label="Created from"
                        class="px-0"
                      />
                      <template #content>
                        <!-- @vue-ignore -->
                        <UCalendar v-model="createdFrom" />
                      </template>
                    </UPopover>
                  </template>
                </UInputDate>
                <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">From</p>
              </UFormField>
              <UFormField name="createdTo">
                <!-- @vue-ignore -->
                <UInputDate v-model="createdTo" class="w-full">
                  <template #trailing>
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="link"
                        size="sm"
                        icon="i-lucide-calendar"
                        aria-label="Created until"
                        class="px-0"
                      />
                      <template #content>
                        <!-- @vue-ignore -->
                        <UCalendar v-model="createdTo" />
                      </template>
                    </UPopover>
                  </template>
                </UInputDate>
                <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">Until</p>
              </UFormField>
            </div>
            <p v-if="!createdRangeValid" class="text-xs text-red-500 dark:text-red-400 m-0">
              The From date is after the To date.
            </p>
            <div class="flex gap-2 flex-wrap">
              <button
                class="h-7 px-2.5 rounded-lg border border-black/[0.08] dark:border-white/[0.09] text-gray-500 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] text-xs transition-colors"
                @click="applyPreset(0)"
              >
                Today
              </button>
              <button
                class="h-7 px-2.5 rounded-lg border border-black/[0.08] dark:border-white/[0.09] text-gray-500 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] text-xs transition-colors"
                @click="applyPreset(7)"
              >
                Last 7 days
              </button>
              <button
                class="h-7 px-2.5 rounded-lg border border-black/[0.08] dark:border-white/[0.09] text-gray-500 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] text-xs transition-colors"
                @click="applyPreset(30)"
              >
                Last 30 days
              </button>
            </div>
          </div>

          <div class="flex flex-col gap-3">
            <p class="text-[13px] font-semibold text-gray-700 dark:text-white/70 m-0">Completed</p>
            <div class="grid grid-cols-2 gap-3">
              <UFormField name="completedFrom">
                <!-- @vue-ignore -->
                <UInputDate v-model="completedFrom" class="w-full">
                  <template #trailing>
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="link"
                        size="sm"
                        icon="i-lucide-calendar"
                        aria-label="Completed from"
                        class="px-0"
                      />
                      <template #content>
                        <!-- @vue-ignore -->
                        <UCalendar v-model="completedFrom" />
                      </template>
                    </UPopover>
                  </template>
                </UInputDate>
                <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">From</p>
              </UFormField>
              <UFormField name="completedTo">
                <!-- @vue-ignore -->
                <UInputDate v-model="completedTo" class="w-full">
                  <template #trailing>
                    <UPopover>
                      <UButton
                        color="neutral"
                        variant="link"
                        size="sm"
                        icon="i-lucide-calendar"
                        aria-label="Completed until"
                        class="px-0"
                      />
                      <template #content>
                        <!-- @vue-ignore -->
                        <UCalendar v-model="completedTo" />
                      </template>
                    </UPopover>
                  </template>
                </UInputDate>
                <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">Until</p>
              </UFormField>
            </div>
            <p v-if="!completedRangeValid" class="text-xs text-red-500 dark:text-red-400 m-0">
              The From date is after the To date.
            </p>
            <p class="text-xs text-gray-400 dark:text-white/30 m-0">
              Jobs that are still running are left out.
            </p>
          </div>

          <div class="flex flex-col gap-3">
            <p class="text-[13px] font-semibold text-gray-700 dark:text-white/70 m-0">Retries</p>
            <div
              class="inline-flex items-center gap-1 self-start p-0.5 rounded-[10px] border border-black/[0.08] dark:border-white/[0.09]"
            >
              <button
                class="flex items-center justify-center w-7 h-7 rounded-lg text-gray-400 dark:text-white/35 hover:bg-black/[0.05] dark:hover:bg-white/[0.07] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                :disabled="minRetries <= 0"
                aria-label="Fewer"
                @click="minRetries--"
              >
                <Icon icon="lucide:minus" class="w-3.5 h-3.5" />
              </button>
              <span
                class="min-w-[84px] text-center text-xs tabular-nums text-gray-600 dark:text-white/50"
              >
                {{ minRetries > 0 ? `At least ${minRetries}` : "Any" }}
              </span>
              <button
                class="flex items-center justify-center w-7 h-7 rounded-lg text-gray-400 dark:text-white/35 hover:bg-black/[0.05] dark:hover:bg-white/[0.07] transition-colors"
                aria-label="More"
                @click="minRetries++"
              >
                <Icon icon="lucide:plus" class="w-3.5 h-3.5" />
              </button>
            </div>
            <p class="text-xs text-gray-400 dark:text-white/30 m-0">
              Show jobs retried this many times or more.
            </p>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="isLoading" class="flex items-center justify-center py-20">
      <Icon icon="mdi:loading" class="w-6 h-6 animate-spin text-gray-400 dark:text-white/35" />
    </div>

    <!-- Error state -->
    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm font-medium text-gray-700 dark:text-white/70 m-0">Could not load jobs</p>
      <p class="text-xs text-gray-400 dark:text-white/30 m-0">
        The jobs service did not respond. Check the connection and try again.
      </p>
      <button
        class="mt-2 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-black/[0.05] dark:bg-white/[0.07] text-gray-600 dark:text-white/55 hover:bg-black/[0.08] dark:hover:bg-white/[0.10] transition-colors"
        @click="handleRefresh"
      >
        <Icon icon="mdi:refresh" class="w-3.5 h-3.5" />
        Try again
      </button>
    </div>

    <!-- Empty states -->
    <div v-else-if="!groups.length" class="flex flex-col items-center gap-3 py-20 text-center">
      <Icon icon="mdi:cog-outline" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm font-medium text-gray-700 dark:text-white/70 m-0">
        {{ isFiltered ? "No jobs match these filters" : "No transpilation jobs yet" }}
      </p>
      <p class="text-xs text-gray-400 dark:text-white/30 m-0">
        {{
          isFiltered
            ? "Try different filters."
            : "Jobs appear here when a file is queued for streaming."
        }}
      </p>
      <button
        v-if="isFiltered"
        class="mt-1 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-black/[0.05] dark:bg-white/[0.07] text-gray-600 dark:text-white/55 hover:bg-black/[0.08] dark:hover:bg-white/[0.10] transition-colors"
        @click="clearFilters"
      >
        Clear filters
      </button>
    </div>

    <!-- Job groups -->
    <div v-else class="space-y-3">
      <div
        v-if="statusFilter === undefined && failedCount > 0"
        class="flex items-center gap-3 px-4 py-3 rounded-xl border border-red-200/60 dark:border-red-500/20 bg-red-50/80 dark:bg-red-500/[0.07]"
      >
        <Icon
          icon="mdi:alert-circle-outline"
          class="w-4 h-4 shrink-0 text-red-500 dark:text-red-400"
        />
        <span class="text-xs text-gray-700 dark:text-white/70">
          {{ failedCount }} job{{ failedCount !== 1 ? "s" : "" }} failed and may need a retry.
        </span>
        <span class="flex-1" />
        <button
          class="px-2.5 py-1 rounded-lg text-xs font-medium text-red-600 dark:text-red-400 hover:bg-red-100/60 dark:hover:bg-red-500/10 transition-colors shrink-0"
          @click="selectStatus(JobStatus.Failed)"
        >
          Review failed
        </button>
      </div>

      <div
        v-for="group in groups"
        :key="group.versionId"
        class="rounded-xl border border-black/[0.08] dark:border-white/[0.09] overflow-hidden frosted-glass glass-surface"
      >
        <!-- Group header -->
        <button
          class="w-full flex items-center gap-3 px-4 py-3 bg-black/[0.03] dark:bg-white/[0.03] hover:bg-black/[0.05] dark:hover:bg-white/[0.05] transition-colors text-left border-b border-black/[0.06] dark:border-white/[0.06]"
          :aria-expanded="isGroupOpen(group)"
          @click="toggleGroup(group)"
        >
          <span
            class="flex items-center justify-center w-9 h-9 rounded-[10px] bg-black/[0.04] dark:bg-white/[0.06] text-gray-500 dark:text-white/40 shrink-0"
          >
            <Icon :icon="group.isVideo ? 'lucide:video' : 'mdi:music-note'" class="w-4 h-4" />
          </span>
          <span class="flex-1 min-w-0">
            <span class="text-sm font-medium text-gray-700 dark:text-white/80 truncate block">
              {{ group.fileName ?? group.versionId }}
            </span>
            <span
              v-if="group.fileName && group.versionNumber != null"
              class="text-xs text-gray-400 dark:text-white/30"
            >
              Version {{ group.versionNumber }}, {{ group.jobs.length }} run{{
                group.jobs.length !== 1 ? "s" : ""
              }}
            </span>
          </span>
          <span class="hidden sm:flex items-center gap-3 flex-wrap justify-end">
            <span
              v-for="item in groupSummary(group)"
              :key="item.status"
              class="inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-white/40 tabular-nums"
            >
              <span class="w-2 h-2 rounded-full" :class="statusConfig[item.status].barClass" />
              {{ item.count }} {{ statusConfig[item.status].label.toLowerCase() }}
            </span>
          </span>
          <Icon
            icon="mdi:chevron-down"
            class="w-4 h-4 text-gray-400 dark:text-white/30 transition-transform duration-200 shrink-0"
            :class="{ 'rotate-180': isGroupOpen(group) }"
          />
        </button>

        <!-- Job rows -->
        <div
          v-if="isGroupOpen(group)"
          class="divide-y divide-black/[0.04] dark:divide-white/[0.05]"
        >
          <div
            v-for="job in group.jobs"
            :key="job.id"
            class="job-grid px-4 py-3 hover:bg-black/[0.01] dark:hover:bg-white/[0.02] transition-colors"
          >
            <span
              class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-medium w-fit"
              :class="statusConfig[job.status].chipClass"
            >
              <Icon
                :icon="statusConfig[job.status].icon"
                class="w-3.5 h-3.5 shrink-0"
                :class="{ 'animate-spin': isSpinning(job.status) }"
              />
              {{ statusConfig[job.status].label }}
            </span>

            <div class="job-mid min-w-0 flex flex-col gap-2">
              <div class="flex gap-1.5 flex-wrap">
                <span
                  v-for="chip in rungChips(job)"
                  :key="chip"
                  class="h-[22px] px-2 rounded-md bg-black/[0.04] dark:bg-white/[0.06] text-gray-500 dark:text-white/40 text-xs inline-flex items-center tabular-nums"
                >
                  {{ chip }}
                </span>
              </div>
              <div v-if="showProgress(job.status)" class="flex items-center gap-2 max-w-90">
                <div class="flex-1 h-1.5 rounded-full bg-black/10 dark:bg-white/10 overflow-hidden">
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    :class="statusConfig[job.status].barClass"
                    :style="{ width: `${job.progressPercent}%` }"
                  />
                </div>
                <span
                  class="text-xs tabular-nums text-gray-400 dark:text-white/30 w-8 text-right shrink-0"
                >
                  {{ job.progressPercent }}%
                </span>
              </div>
            </div>

            <div
              class="job-meta flex items-center gap-3 text-xs text-gray-400 dark:text-white/30 whitespace-nowrap tabular-nums"
            >
              <span
                v-if="job.retryCount > 0"
                class="inline-flex items-center gap-1 text-amber-600 dark:text-amber-400"
                title="Retry count"
              >
                <Icon icon="mdi:refresh" class="w-3.5 h-3.5" />
                {{ job.retryCount }}
              </span>
              <span :title="formatDate(job.createdAt)">{{ formatRelative(job.createdAt) }}</span>
            </div>

            <div class="job-actions flex items-center gap-2 justify-end">
              <button
                v-if="job.errorDetail"
                class="inline-flex items-center text-xs text-red-500 dark:text-red-400 hover:text-red-600 dark:hover:text-red-300 transition-colors shrink-0"
                @click="toggleError(job.id)"
              >
                {{ expandedError[job.id] ? "Hide error" : "Show error" }}
              </button>

              <button
                v-if="canCancel(job.status)"
                class="inline-flex items-center gap-1.5 h-8 px-3 rounded-lg text-[13px] font-medium border border-black/[0.08] dark:border-white/[0.09] text-gray-700 dark:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                :disabled="actionInProgress === job.id"
                @click="handleCancel(job)"
              >
                <Icon
                  :icon="actionInProgress === job.id ? 'mdi:loading' : 'mdi:close-circle'"
                  class="w-3.5 h-3.5"
                  :class="{ 'animate-spin': actionInProgress === job.id }"
                />
                Cancel
              </button>

              <UPopover v-if="canRequeue(job.status)" v-model:open="requeuePopoverOpen[job.id]">
                <button
                  class="inline-flex items-center gap-1.5 h-8 px-3 rounded-lg text-[13px] font-medium border border-black/[0.08] dark:border-white/[0.09] text-gray-700 dark:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                  :disabled="actionInProgress === job.id"
                  @click="initRequeueRungs(job)"
                >
                  <Icon
                    :icon="actionInProgress === job.id ? 'mdi:loading' : 'mdi:refresh'"
                    class="w-3.5 h-3.5"
                    :class="{ 'animate-spin': actionInProgress === job.id }"
                  />
                  Requeue
                </button>

                <template #content>
                  <div class="p-4 w-60 space-y-3 frosted-glass glass-surface-strong">
                    <p class="text-xs font-semibold text-gray-700 dark:text-white/70 m-0">
                      Adjust qualities
                    </p>
                    <div class="flex flex-wrap gap-1.5">
                      <template v-if="job.isVideo">
                        <button
                          v-for="rung in videoRungOptions"
                          :key="rung.value"
                          class="px-2 py-0.5 rounded text-xs border transition-all"
                          :class="
                            pendingVideoRungs[job.id]?.includes(rung.value)
                              ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                              : 'border-gray-200 dark:border-white/10 text-gray-500 dark:text-white/40 hover:border-gray-300 dark:hover:border-white/20'
                          "
                          @click="toggleVideoRung(job.id, rung.value)"
                        >
                          {{ rung.label }}
                        </button>
                      </template>
                      <template v-else>
                        <button
                          v-for="rung in audioRungOptions"
                          :key="rung.value"
                          class="px-2 py-0.5 rounded text-xs border transition-all"
                          :class="
                            pendingAudioRungs[job.id]?.includes(rung.value)
                              ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                              : 'border-gray-200 dark:border-white/10 text-gray-500 dark:text-white/40 hover:border-gray-300 dark:hover:border-white/20'
                          "
                          @click="toggleAudioRung(job.id, rung.value)"
                        >
                          {{ rung.label }}
                        </button>
                      </template>
                    </div>
                    <div
                      class="flex justify-end pt-1 border-t border-gray-200/70 dark:border-white/[0.08]"
                    >
                      <button
                        class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-primary text-white hover:bg-primary/90 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                        :disabled="
                          actionInProgress === job.id ||
                          (job.isVideo
                            ? !pendingVideoRungs[job.id]?.length
                            : !pendingAudioRungs[job.id]?.length)
                        "
                        @click="handleRequeue(job)"
                      >
                        <Icon
                          :icon="actionInProgress === job.id ? 'mdi:loading' : 'mdi:refresh'"
                          class="w-3.5 h-3.5"
                          :class="{ 'animate-spin': actionInProgress === job.id }"
                        />
                        Requeue
                      </button>
                    </div>
                  </div>
                </template>
              </UPopover>
            </div>

            <!-- Error detail panel -->
            <Transition
              enter-active-class="transition-all duration-200 ease-out"
              leave-active-class="transition-all duration-150 ease-in"
              enter-from-class="opacity-0 -translate-y-1"
              leave-to-class="opacity-0 -translate-y-1"
            >
              <div
                v-if="job.errorDetail && expandedError[job.id]"
                class="job-error-panel rounded-[10px] border border-red-200/60 dark:border-red-500/20 bg-red-50/80 dark:bg-red-500/[0.07] overflow-hidden"
              >
                <div
                  class="flex items-center justify-between pl-3 pr-2 py-1.5 border-b border-red-200/40 dark:border-red-500/15"
                >
                  <span class="text-xs font-medium text-red-600 dark:text-red-400"
                    >Error detail</span
                  >
                  <button
                    class="flex items-center gap-1 text-xs text-red-500 dark:text-red-400 hover:text-red-700 dark:hover:text-red-300 transition-colors px-1.5 py-0.5 rounded hover:bg-red-100/60 dark:hover:bg-red-500/10"
                    @click="copyError(job.errorDetail!)"
                  >
                    <Icon icon="mdi:content-copy" class="w-3.5 h-3.5" />
                    Copy
                  </button>
                </div>
                <pre
                  class="px-3 py-3 text-xs text-red-700 dark:text-red-300 whitespace-pre-wrap break-all font-mono leading-relaxed m-0 max-h-44 overflow-y-auto"
                  >{{ job.errorDetail }}</pre>
              </div>
            </Transition>
          </div>
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div
      v-if="data && (data.totalPages ?? 1) > 1"
      class="flex items-center justify-center gap-2 mt-6"
    >
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.05] dark:bg-white/[0.07] hover:bg-black/[0.08] dark:hover:bg-white/[0.10] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="currentPage <= 1"
        @click="currentPage--"
      >
        <Icon icon="mdi:chevron-left" class="w-4 h-4" /> Prev
      </button>
      <span class="text-sm text-gray-400 dark:text-white/35 tabular-nums px-2">
        {{ currentPage }} / {{ data.totalPages }}
      </span>
      <button
        class="flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm text-gray-600 dark:text-white/55 bg-black/[0.05] dark:bg-white/[0.07] hover:bg-black/[0.08] dark:hover:bg-white/[0.10] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        :disabled="currentPage >= (data.totalPages ?? 1)"
        @click="currentPage++"
      >
        Next <Icon icon="mdi:chevron-right" class="w-4 h-4" />
      </button>
    </div>
  </div>
</template>

<style scoped>
/* Status tabs: underline style with horizontal scroll on mobile */
.filter-tabs {
  display: flex;
  gap: 1.5rem;
  margin-bottom: 1rem;
  border-bottom: 1px solid rgb(0 0 0 / 0.06);
  overflow-x: auto;
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.dark .filter-tabs {
  border-bottom-color: rgb(255 255 255 / 0.07);
}

.filter-tabs::-webkit-scrollbar {
  display: none;
}

.filter-tab {
  position: relative;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  height: 2.75rem;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
  color: rgb(75 85 99);
  transition: color 0.15s;
}

.dark .filter-tab {
  color: rgb(255 255 255 / 0.4);
}

.filter-tab:hover {
  color: rgb(17 24 39);
}

.dark .filter-tab:hover {
  color: rgb(255 255 255 / 0.7);
}

.filter-tab-on {
  font-weight: 600;
  color: rgb(17 24 39);
}

.dark .filter-tab-on {
  color: rgb(255 255 255 / 0.9);
}

.filter-tab em {
  font-style: normal;
  font-size: 0.75rem;
  font-variant-numeric: tabular-nums;
}

/* More-filters panel grid */
.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 2rem;
}

/* Filters toolbar: side by side on desktop, wraps on mobile */
.filters-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

@media (max-width: 639px) {
  .filters-toolbar {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
    gap: 0.375rem;
  }

  .filter-group {
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
    padding-bottom: 2px;
  }

  .filter-group::-webkit-scrollbar {
    display: none;
  }

  /* Remove the desktop divider between the two filter groups */
  .filter-group.border-l {
    border-left: none;
    padding-left: 0;
  }
}

/* Job row grid: badge | qualities+progress | meta | actions */
.job-grid {
  display: grid;
  grid-template-columns: 116px minmax(0, 1fr) auto auto;
  gap: 0.5rem 1rem;
  align-items: center;
}

.job-error-panel {
  grid-column: 1 / -1;
}

@media (max-width: 639px) {
  .job-grid {
    grid-template-columns: 1fr auto;
  }

  .job-mid,
  .job-meta,
  .job-error-panel {
    grid-column: 1 / -1;
  }

  .job-actions {
    grid-row: 1;
    grid-column: 2;
  }
}
</style>
