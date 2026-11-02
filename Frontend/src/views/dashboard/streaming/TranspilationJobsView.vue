<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { getLocalTimeZone, today } from "@internationalized/date";
import { useQuery } from "@pinia/colada";
import { computed, reactive, ref, watch } from "vue";

import type { TranspilationJobQuery, TranspilationJobResponse } from "@/api/streaming";

import { AudioRung, VideoRung } from "@/api/policy";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
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
    icon: "lucide:clock",
    chipClass: "bg-gray-200/80 dark:bg-white/10 text-gray-600 dark:text-gray-400",
    barClass: "bg-gray-400 dark:bg-white/30",
  },
  [JobStatus.Partial]: {
    label: "Partial",
    icon: "lucide:clock-alert",
    chipClass: "bg-amber-100 dark:bg-amber-500/15 text-amber-700 dark:text-amber-400",
    barClass: "bg-amber-400 dark:bg-amber-500",
  },
  [JobStatus.Processing]: {
    label: "Processing",
    icon: "lucide:loader-circle",
    chipClass: "bg-blue-100 dark:bg-blue-500/15 text-blue-700 dark:text-blue-400",
    barClass: "bg-blue-400 dark:bg-blue-500",
  },
  [JobStatus.CancellationRequested]: {
    label: "Cancelling",
    icon: "lucide:loader-circle",
    chipClass: "bg-orange-100 dark:bg-orange-500/15 text-orange-600 dark:text-orange-400",
    barClass: "bg-orange-400 dark:bg-orange-500",
  },
  [JobStatus.Cancelled]: {
    label: "Cancelled",
    icon: "lucide:circle-x",
    chipClass: "bg-gray-200/80 dark:bg-white/[0.07] text-gray-600 dark:text-gray-400",
    barClass: "bg-gray-300 dark:bg-white/20",
  },
  [JobStatus.Ready]: {
    label: "Ready",
    icon: "lucide:circle-check",
    chipClass: "bg-emerald-100 dark:bg-emerald-500/15 text-emerald-700 dark:text-emerald-400",
    barClass: "bg-emerald-400 dark:bg-emerald-500",
  },
  [JobStatus.Failed]: {
    label: "Failed",
    icon: "lucide:circle-alert",
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
  { label: "All", value: undefined, icon: "lucide:infinity" },
  { label: "Video", value: true, icon: "lucide:video" },
  { label: "Audio", value: false, icon: "lucide:music" },
];
</script>

<template>
  <div class="flex w-full flex-col gap-3 p-3">
    <header
      class="rounded-xl border border-gray-200/70 frosted-glass glass-surface-strong dark:border-gray-700/70"
    >
      <!-- Title row -->
      <div class="flex flex-wrap items-center gap-4 px-4 py-4">
        <div class="flex min-w-0 flex-col gap-0.5">
          <h1 class="m-0 text-xl font-semibold text-gray-900 dark:text-gray-100">
            Transpilation Jobs
          </h1>
          <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
            {{ groups.length }} file{{ groups.length !== 1 ? "s" : "" }},
            {{ data?.totalCount ?? 0 }} job{{ (data?.totalCount ?? 0) !== 1 ? "s" : "" }}
          </p>
        </div>
        <span
          v-if="hasLive && !isLoading"
          class="inline-flex h-6 items-center gap-2 rounded-full border border-gray-200/70 px-3 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
        >
          <span class="h-2 w-2 animate-pulse rounded-full bg-blue-500" />
          Updating live
        </span>
        <span class="flex-1" />
        <UTooltip text="Refresh jobs">
          <UButton
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="ghost"
            size="sm"
            class="justify-center"
            aria-label="Refresh jobs"
            :loading="isRefreshing"
            :disabled="isLoading"
            @click="handleRefresh"
          />
        </UTooltip>
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
              class="absolute bottom-0 left-0 right-0 h-0.5 rounded-full bg-primary"
            />
          </button>
        </div>

        <div class="filters-toolbar">
          <div
            class="filter-group inline-flex h-8 items-center gap-0.5 rounded-lg border border-gray-200/70 px-0.5 dark:border-gray-700/70"
            role="group"
            aria-label="Media type"
          >
            <button
              v-for="opt in mediaFilterOptions"
              :key="String(opt.value)"
              class="inline-flex h-6 items-center gap-1 whitespace-nowrap rounded-md px-3 text-xs font-medium transition-colors"
              :class="
                isVideoFilter === opt.value
                  ? 'bg-primary text-white'
                  : 'text-gray-600 hover:bg-black/[0.05] dark:text-gray-400 dark:hover:bg-white/[0.07]'
              "
              :aria-pressed="isVideoFilter === opt.value"
              @click="selectMedia(opt.value)"
            >
              <Icon :icon="opt.icon" class="w-3.5 h-3.5" />
              {{ opt.label }}
            </button>
          </div>

          <UButton
            icon="i-lucide-filter"
            size="sm"
            class="shrink-0"
            :color="filtersOpen || advancedFilterCount > 0 ? 'primary' : 'neutral'"
            :variant="filtersOpen || advancedFilterCount > 0 ? 'subtle' : 'outline'"
            :aria-expanded="filtersOpen"
            @click="filtersOpen = !filtersOpen"
          >
            Filters
            <UBadge v-if="advancedFilterCount > 0" color="primary" size="sm" class="tabular-nums">
              {{ advancedFilterCount }}
            </UBadge>
          </UButton>

          <span
            v-for="chip in activeChips"
            :key="chip.key"
            class="inline-flex h-6 items-center gap-1 whitespace-nowrap rounded-full border border-gray-200/70 pl-3 pr-1 text-xs text-gray-900 dark:border-gray-700/70 dark:text-gray-100"
          >
            {{ chip.label }}
            <button
              class="grid h-5 w-5 place-items-center rounded-full text-gray-500 transition-colors hover:text-gray-900 dark:text-gray-500 dark:hover:text-gray-100"
              :aria-label="`Remove filter ${chip.label}`"
              @click="removeChip(chip.key)"
            >
              <Icon icon="lucide:x" class="w-3.5 h-3.5" />
            </button>
          </span>
          <UButton
            v-if="activeChips.length > 0"
            color="neutral"
            variant="ghost"
            size="xs"
            class="whitespace-nowrap"
            @click="resetAdvanced"
          >
            Clear all
          </UButton>
        </div>

        <!-- More filters panel -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0 -translate-y-1"
          leave-active-class="transition-all duration-150 ease-in"
          leave-to-class="opacity-0 -translate-y-1"
        >
          <div
            v-if="filtersOpen"
            class="border-t border-gray-200/70 p-4 dark:border-gray-700/70"
          >
            <div class="mb-4 flex items-center gap-2">
              <p class="m-0 text-sm font-semibold text-gray-900 dark:text-gray-100">
                More filters
              </p>
              <span class="flex-1" />
              <UButton
                color="neutral"
                variant="ghost"
                size="xs"
                :disabled="advancedFilterCount === 0"
                @click="resetAdvanced"
              >
                Reset
              </UButton>
              <UButton
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                size="xs"
                class="justify-center"
                aria-label="Close filters"
                @click="filtersOpen = false"
              />
            </div>

            <div class="filter-grid">
              <div class="flex flex-col gap-2">
                <p class="m-0 text-sm font-semibold text-gray-900 dark:text-gray-100">Created</p>
                <div class="grid grid-cols-2 gap-2">
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
                    <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">From</p>
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
                    <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Until</p>
                  </UFormField>
                </div>
                <p v-if="!createdRangeValid" class="m-0 text-xs text-red-600 dark:text-red-400">
                  The From date is after the To date.
                </p>
                <div class="flex flex-wrap gap-2">
                  <UButton color="neutral" variant="outline" size="xs" @click="applyPreset(0)">
                    Today
                  </UButton>
                  <UButton color="neutral" variant="outline" size="xs" @click="applyPreset(7)">
                    Last 7 days
                  </UButton>
                  <UButton color="neutral" variant="outline" size="xs" @click="applyPreset(30)">
                    Last 30 days
                  </UButton>
                </div>
              </div>

              <div class="flex flex-col gap-2">
                <p class="m-0 text-sm font-semibold text-gray-900 dark:text-gray-100">Completed</p>
                <div class="grid grid-cols-2 gap-2">
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
                    <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">From</p>
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
                    <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Until</p>
                  </UFormField>
                </div>
                <p v-if="!completedRangeValid" class="m-0 text-xs text-red-600 dark:text-red-400">
                  The From date is after the To date.
                </p>
                <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
                  Jobs that are still running are left out.
                </p>
              </div>

              <div class="flex flex-col gap-2">
                <p class="m-0 text-sm font-semibold text-gray-900 dark:text-gray-100">Retries</p>
                <div
                  class="inline-flex items-center gap-1 self-start rounded-lg border border-gray-200/70 p-0.5 dark:border-gray-700/70"
                >
                  <UButton
                    icon="i-lucide-minus"
                    color="neutral"
                    variant="ghost"
                    size="xs"
                    class="justify-center"
                    aria-label="Fewer"
                    :disabled="minRetries <= 0"
                    @click="minRetries--"
                  />
                  <span
                    class="min-w-24 text-center text-xs tabular-nums text-gray-600 dark:text-gray-400"
                  >
                    {{ minRetries > 0 ? `At least ${minRetries}` : "Any" }}
                  </span>
                  <UButton
                    icon="i-lucide-plus"
                    color="neutral"
                    variant="ghost"
                    size="xs"
                    class="justify-center"
                    aria-label="More"
                    @click="minRetries++"
                  />
                </div>
                <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
                  Show jobs retried this many times or more.
                </p>
              </div>
            </div>
          </div>
        </Transition>
      </div>
    </header>

    <!-- Jobs panel -->
    <section
      class="overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    >
      <!-- Loading state -->
      <div v-if="isLoading" class="flex items-center justify-center py-20">
        <BlockSpinner :size="24" class="text-gray-600 dark:text-gray-400" />
      </div>

      <!-- Error state -->
      <div
        v-else-if="error"
        class="flex flex-col items-center gap-3 px-4 py-20 text-center"
      >
        <Icon icon="lucide:circle-alert" class="w-12 h-12 text-red-400" />
        <p class="m-0 text-sm font-medium text-gray-900 dark:text-gray-100">Could not load jobs</p>
        <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
          The jobs service did not respond. Check the connection and try again.
        </p>
        <UButton
          icon="i-lucide-refresh-cw"
          color="neutral"
          variant="outline"
          size="sm"
          @click="handleRefresh"
        >
          Try again
        </UButton>
      </div>

      <!-- Empty states -->
      <div
        v-else-if="!groups.length"
        class="flex flex-col items-center gap-3 px-4 py-20 text-center"
      >
        <Icon icon="lucide:cog" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <p class="m-0 text-sm font-medium text-gray-900 dark:text-gray-100">
          {{ isFiltered ? "No jobs match these filters" : "No transpilation jobs yet" }}
        </p>
        <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
          {{
            isFiltered
              ? "Try different filters."
              : "Jobs appear here when a file is queued for streaming."
          }}
        </p>
        <UButton
          v-if="isFiltered"
          color="neutral"
          variant="outline"
          size="sm"
          @click="clearFilters"
        >
          Clear filters
        </UButton>
      </div>

      <!-- Job groups -->
      <div v-else>
        <div
          v-if="statusFilter === undefined && failedCount > 0"
          class="flex items-center gap-2 border-b border-red-200/60 bg-red-50/80 px-4 py-2 dark:border-red-500/20 dark:bg-red-500/[0.07]"
        >
          <Icon
            icon="lucide:circle-alert"
            class="w-4 h-4 shrink-0 text-red-600 dark:text-red-400"
          />
          <span class="text-xs text-gray-900 dark:text-gray-100">
            {{ failedCount }} job{{ failedCount !== 1 ? "s" : "" }} failed and may need a retry.
          </span>
          <span class="flex-1" />
          <UButton
            color="neutral"
            variant="outline"
            size="xs"
            class="shrink-0"
            @click="selectStatus(JobStatus.Failed)"
          >
            Review failed
          </UButton>
        </div>

        <div class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
          <div v-for="group in groups" :key="group.versionId">
            <!-- Group header -->
            <button
              class="flex w-full items-center gap-4 px-4 py-3 text-left transition-colors hover:bg-black/[0.03] dark:hover:bg-white/[0.04]"
              :aria-expanded="isGroupOpen(group)"
              @click="toggleGroup(group)"
            >
              <span
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-gray-200/70 text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
              >
                <Icon :icon="group.isVideo ? 'lucide:video' : 'lucide:music'" class="w-4 h-4" />
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                  {{ group.fileName ?? group.versionId }}
                </span>
                <span
                  v-if="group.fileName && group.versionNumber != null"
                  class="text-xs text-gray-600 dark:text-gray-400"
                >
                  Version {{ group.versionNumber }}, {{ group.jobs.length }} run{{
                    group.jobs.length !== 1 ? "s" : ""
                  }}
                </span>
              </span>
              <span class="hidden flex-wrap items-center justify-end gap-4 sm:flex">
                <span
                  v-for="item in groupSummary(group)"
                  :key="item.status"
                  class="inline-flex items-center gap-2 text-xs tabular-nums text-gray-600 dark:text-gray-400"
                >
                  <span class="h-2 w-2 rounded-full" :class="statusConfig[item.status].barClass" />
                  {{ item.count }} {{ statusConfig[item.status].label.toLowerCase() }}
                </span>
              </span>
              <Icon
                icon="lucide:chevron-down"
                class="w-4 h-4 shrink-0 text-gray-500 transition-transform duration-200 dark:text-gray-500"
                :class="{ 'rotate-180': isGroupOpen(group) }"
              />
            </button>

            <!-- Job rows -->
            <div
              v-if="isGroupOpen(group)"
              class="divide-y divide-gray-100/50 border-t border-gray-200/70 dark:divide-gray-800/50 dark:border-gray-700/70"
            >
              <div
                v-for="job in group.jobs"
                :key="job.id"
                class="job-grid px-4 py-3 transition-colors hover:bg-black/[0.02] sm:pl-16 dark:hover:bg-white/[0.03]"
              >
                <span
                  class="inline-flex w-fit items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-medium"
                  :class="statusConfig[job.status].chipClass"
                >
                  <Icon
                    :icon="statusConfig[job.status].icon"
                    class="w-3.5 h-3.5 shrink-0"
                    :class="{ 'animate-spin': isSpinning(job.status) }"
                  />
                  {{ statusConfig[job.status].label }}
                </span>

                <div class="job-mid flex min-w-0 flex-col gap-2">
                  <div class="flex flex-wrap gap-2">
                    <span
                      v-for="chip in rungChips(job)"
                      :key="chip"
                      class="inline-flex h-6 items-center rounded-md border border-gray-200/70 px-2 text-xs tabular-nums text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
                    >
                      {{ chip }}
                    </span>
                  </div>
                  <div v-if="showProgress(job.status)" class="flex max-w-90 items-center gap-2">
                    <div
                      class="h-1.5 flex-1 overflow-hidden rounded-full bg-black/10 dark:bg-white/10"
                    >
                      <div
                        class="h-full rounded-full transition-all duration-500"
                        :class="statusConfig[job.status].barClass"
                        :style="{ width: `${job.progressPercent}%` }"
                      />
                    </div>
                    <span
                      class="w-8 shrink-0 text-right text-xs tabular-nums text-gray-600 dark:text-gray-400"
                    >
                      {{ job.progressPercent }}%
                    </span>
                  </div>
                </div>

                <div
                  class="job-meta flex items-center gap-4 whitespace-nowrap text-xs tabular-nums text-gray-600 dark:text-gray-400"
                >
                  <span
                    v-if="job.retryCount > 0"
                    class="inline-flex items-center gap-1 text-amber-600 dark:text-amber-400"
                    title="Retry count"
                  >
                    <Icon icon="lucide:rotate-ccw" class="w-3.5 h-3.5" />
                    {{ job.retryCount }}
                  </span>
                  <span :title="formatDate(job.createdAt)">
                    {{ formatRelative(job.createdAt) }}
                  </span>
                </div>

                <div class="job-actions flex items-center justify-end gap-2">
                  <button
                    v-if="job.errorDetail"
                    class="shrink-0 text-xs text-red-600 transition-colors hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
                    @click="toggleError(job.id)"
                  >
                    {{ expandedError[job.id] ? "Hide error" : "Show error" }}
                  </button>

                  <UButton
                    v-if="canCancel(job.status)"
                    icon="i-lucide-circle-x"
                    color="neutral"
                    variant="outline"
                    size="sm"
                    :loading="actionInProgress === job.id"
                    @click="handleCancel(job)"
                  >
                    Cancel
                  </UButton>

                  <UPopover v-if="canRequeue(job.status)" v-model:open="requeuePopoverOpen[job.id]">
                    <UButton
                      icon="i-lucide-refresh-cw"
                      color="neutral"
                      variant="outline"
                      size="sm"
                      :loading="actionInProgress === job.id"
                      @click="initRequeueRungs(job)"
                    >
                      Requeue
                    </UButton>

                    <template #content>
                      <div class="w-60 space-y-3 p-4 frosted-glass glass-surface-strong">
                        <p class="m-0 text-xs font-semibold text-gray-900 dark:text-gray-100">
                          Adjust qualities
                        </p>
                        <div class="flex flex-wrap gap-2">
                          <template v-if="job.isVideo">
                            <button
                              v-for="rung in videoRungOptions"
                              :key="rung.value"
                              class="rounded-md border px-2 py-0.5 text-xs transition-all"
                              :class="
                                pendingVideoRungs[job.id]?.includes(rung.value)
                                  ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                                  : 'border-gray-200/70 text-gray-600 hover:border-gray-300 dark:border-gray-700/70 dark:text-gray-400 dark:hover:border-gray-600'
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
                              class="rounded-md border px-2 py-0.5 text-xs transition-all"
                              :class="
                                pendingAudioRungs[job.id]?.includes(rung.value)
                                  ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                                  : 'border-gray-200/70 text-gray-600 hover:border-gray-300 dark:border-gray-700/70 dark:text-gray-400 dark:hover:border-gray-600'
                              "
                              @click="toggleAudioRung(job.id, rung.value)"
                            >
                              {{ rung.label }}
                            </button>
                          </template>
                        </div>
                        <div
                          class="flex justify-end border-t border-gray-200/70 pt-3 dark:border-gray-700/70"
                        >
                          <UButton
                            icon="i-lucide-refresh-cw"
                            color="primary"
                            variant="solid"
                            size="xs"
                            :loading="actionInProgress === job.id"
                            :disabled="
                              job.isVideo
                                ? !pendingVideoRungs[job.id]?.length
                                : !pendingAudioRungs[job.id]?.length
                            "
                            @click="handleRequeue(job)"
                          >
                            Requeue
                          </UButton>
                        </div>
                      </div>
                    </template>
                  </UPopover>
                </div>

                <!-- Error detail panel -->
                <Transition
                  enter-active-class="transition-all duration-200 ease-out"
                  enter-from-class="opacity-0 -translate-y-1"
                  leave-active-class="transition-all duration-150 ease-in"
                  leave-to-class="opacity-0 -translate-y-1"
                >
                  <div
                    v-if="job.errorDetail && expandedError[job.id]"
                    class="job-error-panel overflow-hidden rounded-lg border border-red-200/60 bg-red-50/80 dark:border-red-500/20 dark:bg-red-500/[0.07]"
                  >
                    <div
                      class="flex items-center justify-between border-b border-red-200/40 py-1 pl-3 pr-2 dark:border-red-500/15"
                    >
                      <span class="text-xs font-medium text-red-600 dark:text-red-400">
                        Error detail
                      </span>
                      <UButton
                        icon="i-lucide-copy"
                        color="error"
                        variant="ghost"
                        size="xs"
                        @click="copyError(job.errorDetail!)"
                      >
                        Copy
                      </UButton>
                    </div>
                    <pre
                      class="m-0 max-h-44 overflow-y-auto whitespace-pre-wrap break-all px-3 py-3 font-mono text-xs leading-relaxed text-red-700 dark:text-red-300"
                      >{{ job.errorDetail }}</pre>
                  </div>
                </Transition>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div
        v-if="data && (data.totalPages ?? 1) > 1"
        class="flex items-center justify-center gap-2 border-t border-gray-200/70 px-4 py-2 dark:border-gray-700/70"
      >
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="outline"
          size="xs"
          :disabled="currentPage <= 1"
          @click="currentPage--"
        >
          Prev
        </UButton>
        <span class="px-2 text-sm tabular-nums text-gray-600 dark:text-gray-400">
          {{ currentPage }} / {{ data.totalPages }}
        </span>
        <UButton
          trailing-icon="i-lucide-chevron-right"
          color="neutral"
          variant="outline"
          size="xs"
          :disabled="currentPage >= (data.totalPages ?? 1)"
          @click="currentPage++"
        >
          Next
        </UButton>
      </div>
    </section>
  </div>
</template>

<style scoped>
/* Status tabs: underline style with horizontal scroll on mobile */
.filter-tabs {
  display: flex;
  gap: 1.5rem;
  padding: 0 1rem;
  border-bottom: 1px solid rgb(229 231 235 / 0.7);
  overflow-x: auto;
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.dark .filter-tabs {
  border-bottom-color: rgb(55 65 81 / 0.7);
}

.filter-tabs::-webkit-scrollbar {
  display: none;
}

.filter-tab {
  position: relative;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  height: 2.5rem;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
  color: rgb(75 85 99);
  transition: color 0.15s;
}

.dark .filter-tab {
  color: rgb(156 163 175);
}

.filter-tab:hover {
  color: rgb(17 24 39);
}

.dark .filter-tab:hover {
  color: rgb(243 244 246);
}

.filter-tab-on {
  font-weight: 600;
  color: rgb(17 24 39);
}

.dark .filter-tab-on {
  color: rgb(243 244 246);
}

.filter-tab em {
  font-style: normal;
  font-size: 0.75rem;
  font-variant-numeric: tabular-nums;
  color: rgb(107 114 128);
}

/* More-filters panel grid */
.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 2rem;
}

/* Filters toolbar: side by side on desktop, stacked on mobile */
.filters-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  padding: 0.5rem 1rem;
}

@media (max-width: 639px) {
  .filters-toolbar {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .filter-group {
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .filter-group::-webkit-scrollbar {
    display: none;
  }
}

/* Job row grid: badge | qualities + progress | meta | actions */
.job-grid {
  display: grid;
  grid-template-columns: 7rem minmax(0, 1fr) auto auto;
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
