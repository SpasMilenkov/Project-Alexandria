<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, reactive, ref } from "vue";

import type { TranspilationJobQuery, TranspilationJobResponse } from "@/api/streaming";

import { AudioRung, VideoRung } from "@/api/policy";
import { useAppToast } from "@/composables/useAppToast";
import { TranspilationStatus } from "@/enums/transpilation-status";
import { stopTranspilationJob } from "@/mutations/streaming";
import { getTranspilationJobs } from "@/queries/streaming";

const toast = useAppToast();

const currentPage = ref(1);
const pageSize = ref(50);
const statusFilter = ref<TranspilationStatus | undefined>(undefined);
const isVideoFilter = ref<boolean | undefined>(undefined);

const query = computed<TranspilationJobQuery>(() => ({
  currentPage: currentPage.value,
  pageSize: pageSize.value,
  status: statusFilter.value,
  isVideo: isVideoFilter.value,
}));

const { data, isLoading, error, refetch } = useQuery(() => getTranspilationJobs(query.value));
const { mutateAsync: updateJobStatus } = stopTranspilationJob();

const isRefreshing = ref(false);

const handleRefresh = async () => {
  isRefreshing.value = true;
  try {
    await refetch();
  } finally {
    isRefreshing.value = false;
  }
};

const patchJobStatus = (jobId: string, status: TranspilationStatus) => {
  if (!data.value) return;
  const item = data.value.items.find((j) => j.id === jobId);
  if (item) item.status = status;
};

const groupedJobs = computed(() => {
  const jobs = data.value?.items ?? [];
  const map = new Map<string, TranspilationJobResponse[]>();
  for (const job of jobs) {
    const existing = map.get(job.versionId) ?? [];
    existing.push(job);
    map.set(job.versionId, existing);
  }
  return [...map.entries()].map(([versionId, jobs]) => ({
    versionId,
    fileName: jobs[0].fileName,
    versionNumber: jobs[0].versionNumber,
    isVideo: jobs[0].isVideo,
    jobs,
  }));
});

const collapsed = reactive<Record<string, boolean>>({});
const toggleGroup = (versionId: string) => {
  collapsed[versionId] = !collapsed[versionId];
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
  if (!pendingAudioRungs[job.id]) pendingAudioRungs[job.id] = [...(job.audioRungs ?? [])];
  if (!pendingVideoRungs[job.id]) pendingVideoRungs[job.id] = [...(job.videoRungs ?? [])];
};

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
    job.status === TranspilationStatus.Processing
      ? TranspilationStatus.CancellationRequested
      : TranspilationStatus.Cancelled;
  try {
    await updateJobStatus({ jobId: job.id, status: targetStatus });
    patchJobStatus(job.id, targetStatus);
    toast.info(
      targetStatus === TranspilationStatus.CancellationRequested
        ? "Cancellation requested"
        : "Job cancelled",
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
      status: TranspilationStatus.Queued,
      audioRungs: job.isVideo ? undefined : pendingAudioRungs[job.id],
      videoRungs: job.isVideo ? pendingVideoRungs[job.id] : undefined,
    });
    patchJobStatus(job.id, TranspilationStatus.Queued);
    requeuePopoverOpen[job.id] = false;
    toast.success("Job requeued");
  } catch (err) {
    toast.error("Failed to requeue job", err);
  } finally {
    actionInProgress.value = null;
  }
};

type StatusConfig = { label: string; icon: string; chipClass: string; barClass: string };

const statusConfig: Record<TranspilationStatus, StatusConfig> = {
  [TranspilationStatus.Queued]: {
    label: "Queued",
    icon: "mdi:clock-outline",
    chipClass: "bg-gray-200/80 dark:bg-white/10 text-gray-500 dark:text-white/50",
    barClass: "bg-gray-400 dark:bg-white/30",
  },
  [TranspilationStatus.Partial]: {
    label: "Partial",
    icon: "mdi:progress-clock",
    chipClass: "bg-amber-100 dark:bg-amber-500/15 text-amber-700 dark:text-amber-400",
    barClass: "bg-amber-400 dark:bg-amber-500",
  },
  [TranspilationStatus.Processing]: {
    label: "Processing",
    icon: "mdi:loading",
    chipClass: "bg-blue-100 dark:bg-blue-500/15 text-blue-700 dark:text-blue-400",
    barClass: "bg-primary",
  },
  [TranspilationStatus.CancellationRequested]: {
    label: "Cancelling",
    icon: "mdi:loading",
    chipClass: "bg-orange-100 dark:bg-orange-500/15 text-orange-600 dark:text-orange-400",
    barClass: "bg-orange-400 dark:bg-orange-500",
  },
  [TranspilationStatus.Cancelled]: {
    label: "Cancelled",
    icon: "mdi:cancel",
    chipClass: "bg-gray-200/80 dark:bg-white/[0.07] text-gray-400 dark:text-white/35",
    barClass: "bg-gray-300 dark:bg-white/20",
  },
  [TranspilationStatus.Ready]: {
    label: "Ready",
    icon: "mdi:check-circle-outline",
    chipClass: "bg-emerald-100 dark:bg-emerald-500/15 text-emerald-700 dark:text-emerald-400",
    barClass: "bg-emerald-400 dark:bg-emerald-500",
  },
  [TranspilationStatus.Failed]: {
    label: "Failed",
    icon: "mdi:alert-circle-outline",
    chipClass: "bg-red-100 dark:bg-red-500/15 text-red-600 dark:text-red-400",
    barClass: "bg-red-400 dark:bg-red-500",
  },
};

const canCancel = (status: TranspilationStatus) =>
  status === TranspilationStatus.Queued ||
  status === TranspilationStatus.Processing ||
  status === TranspilationStatus.Partial;

const canRequeue = (status: TranspilationStatus) =>
  status === TranspilationStatus.Failed ||
  status === TranspilationStatus.Cancelled ||
  status === TranspilationStatus.Ready;

const isSpinning = (status: TranspilationStatus) =>
  status === TranspilationStatus.Processing || status === TranspilationStatus.CancellationRequested;

const formatDate = (iso?: string) =>
  iso
    ? new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
        new Date(iso),
      )
    : "—";

const formatRungs = (job: TranspilationJobResponse) => {
  if (job.isVideo) {
    const labels: Record<number, string> = {
      0: "360p",
      1: "480p",
      2: "720p",
      3: "1080p",
      4: "1440p",
      5: "2160p",
    };
    return (job.videoRungs ?? []).map((r) => labels[r] ?? `${r}`).join(", ") || "—";
  }
  const labels: Record<number, string> = { 0: "96k", 1: "128k", 2: "192k", 3: "256k", 4: "320k" };
  return (job.audioRungs ?? []).map((r) => labels[r] ?? `${r}`).join(", ") || "—";
};

const statusFilterOptions = [
  { label: "All", value: undefined },
  { label: "Queued", value: TranspilationStatus.Queued },
  { label: "Partial", value: TranspilationStatus.Partial },
  { label: "Processing", value: TranspilationStatus.Processing },
  { label: "Ready", value: TranspilationStatus.Ready },
  { label: "Failed", value: TranspilationStatus.Failed },
  { label: "Cancelled", value: TranspilationStatus.Cancelled },
];

const mediaFilterOptions = [
  { label: "All", value: undefined, icon: "mdi:all-inclusive" },
  { label: "Video", value: true, icon: "mdi:file-video-outline" },
  { label: "Audio", value: false, icon: "mdi:music-note-outline" },
];
</script>

<template>
  <div class="px-6 py-8">
    <!-- Page header -->
    <div class="flex items-center justify-between mb-6 gap-4 flex-wrap">
      <div class="flex items-center gap-2">
        <div>
          <h1 class="text-lg font-semibold text-gray-800 dark:text-white/90 m-0">
            Transpilation Jobs
          </h1>
          <p class="text-xs text-gray-400 dark:text-white/30 mt-0.5 m-0">
            {{ groupedJobs.length }} file{{ groupedJobs.length !== 1 ? "s" : "" }},
            {{ data?.totalCount ?? 0 }} job{{ (data?.totalCount ?? 0) !== 1 ? "s" : "" }}
          </p>
        </div>
        <button
          class="flex items-center justify-center w-7 h-7 rounded-lg text-gray-400 dark:text-white/35 hover:bg-black/[0.05] dark:hover:bg-white/[0.07] hover:text-gray-600 dark:hover:text-white/55 transition-colors disabled:opacity-40 disabled:cursor-not-allowed mt-0.5"
          :disabled="isRefreshing || isLoading"
          :title="isRefreshing ? 'Refreshing…' : 'Refresh jobs'"
          @click="handleRefresh"
        >
          <Icon icon="mdi:refresh" class="w-4 h-4" :class="{ 'animate-spin': isRefreshing }" />
        </button>
      </div>

      <!-- Filters — scroll horizontally on mobile, side by side on desktop -->
      <div class="filters-toolbar">
        <div class="flex items-center gap-1 filter-group flex-wrap sm:flex-nowrap">
          <button
            v-for="opt in statusFilterOptions"
            :key="String(opt.value)"
            class="px-3 py-1.5 rounded-lg text-xs font-medium transition-colors whitespace-nowrap"
            :class="
              statusFilter === opt.value
                ? 'bg-primary text-white'
                : 'bg-black/[0.04] dark:bg-white/[0.06] text-gray-600 dark:text-white/50 hover:bg-black/[0.07] dark:hover:bg-white/[0.09]'
            "
            @click="
              statusFilter = opt.value;
              currentPage = 1;
            "
          >
            {{ opt.label }}
          </button>
        </div>

        <div
          class="flex items-center flex-wrap sm:flex-nowrap gap-1 filter-group border-l border-black/[0.06] dark:border-white/[0.07]"
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
            @click="
              isVideoFilter = opt.value;
              currentPage = 1;
            "
          >
            <Icon :icon="opt.icon" class="w-3.5 h-3.5" />
            {{ opt.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- Loading skeletons -->
    <div v-if="isLoading" class="space-y-3">
      <div
        v-for="i in 5"
        :key="i"
        class="h-14 rounded-xl bg-gray-100/60 dark:bg-white/[0.03] animate-pulse"
      />
    </div>

    <!-- Error state -->
    <div v-else-if="error" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <Icon icon="mdi:alert-circle-outline" class="w-9 h-9 text-red-400" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">Failed to load jobs</p>
    </div>

    <!-- Empty state -->
    <div v-else-if="!groupedJobs.length" class="flex flex-col items-center gap-3 py-20 text-center">
      <Icon icon="mdi:cog-outline" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
      <p class="text-sm text-gray-500 dark:text-white/40 m-0">No jobs found</p>
    </div>

    <!-- Job groups -->
    <div v-else class="space-y-3">
      <div
        v-for="group in groupedJobs"
        :key="group.versionId"
        class="rounded-xl border border-black/[0.08] dark:border-white/[0.09] overflow-hidden bg-white/70 dark:bg-white/[0.04] backdrop-blur-sm"
      >
        <!-- Group header -->
        <button
          class="w-full flex items-center gap-3 px-4 py-3 bg-black/[0.03] dark:bg-white/[0.03] hover:bg-black/[0.05] dark:hover:bg-white/[0.05] transition-colors text-left border-b border-black/[0.06] dark:border-white/[0.06]"
          @click="toggleGroup(group.versionId)"
        >
          <Icon
            :icon="group.isVideo ? 'mdi:file-video-outline' : 'mdi:music-box-outline'"
            class="w-4 h-4 shrink-0 text-primary"
          />
          <div class="flex-1 min-w-0">
            <span class="text-sm font-medium text-gray-700 dark:text-white/80 truncate block">
              {{ group.fileName ?? group.versionId }}
            </span>
            <span
              v-if="group.fileName && group.versionNumber != null"
              class="text-xs text-gray-400 dark:text-white/30"
            >
              Version {{ group.versionNumber }}
            </span>
          </div>
          <span class="text-xs text-gray-400 dark:text-white/30 shrink-0">
            {{ group.jobs.length }} run{{ group.jobs.length !== 1 ? "s" : "" }}
          </span>
          <Icon
            icon="mdi:chevron-down"
            class="w-4 h-4 text-gray-400 dark:text-white/30 transition-transform duration-200 shrink-0"
            :class="{ 'rotate-180': !collapsed[group.versionId] }"
          />
        </button>

        <!-- Job rows -->
        <div
          v-show="!collapsed[group.versionId]"
          class="divide-y divide-black/[0.04] dark:divide-white/[0.05]"
        >
          <div
            v-for="job in group.jobs"
            :key="job.id"
            class="border-b border-black/[0.04] dark:border-white/[0.05] last:border-b-0"
          >
            <!-- Single job row: wraps into two lines on mobile via job-row class -->
            <div
              class="job-row px-4 py-3 hover:bg-black/[0.01] dark:hover:bg-white/[0.02] transition-colors"
            >
              <span
                class="job-status inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-medium shrink-0"
                :class="statusConfig[job.status].chipClass"
              >
                <Icon
                  :icon="statusConfig[job.status].icon"
                  class="w-3.5 h-3.5 shrink-0"
                  :class="{ 'animate-spin': isSpinning(job.status) }"
                />
                {{ statusConfig[job.status].label }}
              </span>

              <div class="job-progress flex items-center gap-2 w-28 shrink-0">
                <div class="flex-1 h-1.5 rounded-full bg-black/10 dark:bg-white/10 overflow-hidden">
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    :class="statusConfig[job.status].barClass"
                    :style="{ width: `${job.progressPercent}%` }"
                  />
                </div>
                <span
                  class="text-xs tabular-nums text-gray-400 dark:text-white/30 w-7 text-right shrink-0"
                >
                  {{ job.progressPercent }}%
                </span>
              </div>

              <span
                class="job-rungs text-xs text-gray-500 dark:text-white/40 truncate flex-1 font-mono"
              >
                {{ formatRungs(job) }}
              </span>

              <span
                v-if="job.retryCount > 0"
                class="job-retry inline-flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400 shrink-0 tabular-nums"
                title="Retry count"
              >
                <Icon icon="mdi:refresh" class="w-3.5 h-3.5" />
                {{ job.retryCount }}
              </span>

              <span class="job-date text-xs text-gray-400 dark:text-white/30 shrink-0 tabular-nums">
                {{ formatDate(job.createdAt) }}
              </span>

              <button
                v-if="job.errorDetail"
                class="job-error-toggle inline-flex items-center gap-1 text-xs text-red-500 dark:text-red-400 shrink-0 hover:text-red-600 dark:hover:text-red-300 transition-colors"
                @click="toggleError(job.id)"
              >
                <Icon icon="mdi:alert-circle-outline" class="w-3.5 h-3.5 shrink-0" />
                <span>{{ expandedError[job.id] ? "Hide error" : "Show error" }}</span>
                <Icon
                  icon="mdi:chevron-down"
                  class="w-3.5 h-3.5 transition-transform duration-150"
                  :class="{ 'rotate-180': expandedError[job.id] }"
                />
              </button>

              <div class="job-actions flex items-center gap-1 shrink-0 ml-auto">
                <button
                  v-if="canCancel(job.status)"
                  class="flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-medium text-gray-500 dark:text-white/40 hover:bg-red-50 dark:hover:bg-red-500/10 hover:text-red-500 dark:hover:text-red-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                  :disabled="actionInProgress === job.id"
                  @click="handleCancel(job)"
                >
                  <Icon
                    :icon="actionInProgress === job.id ? 'mdi:loading' : 'mdi:cancel'"
                    class="w-3.5 h-3.5"
                    :class="{ 'animate-spin': actionInProgress === job.id }"
                  />
                  Cancel
                </button>

                <UPopover v-if="canRequeue(job.status)" v-model:open="requeuePopoverOpen[job.id]">
                  <button
                    class="flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-medium text-gray-500 dark:text-white/40 hover:bg-primary/10 hover:text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
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
                    <div
                      class="p-4 w-60 space-y-3 bg-white/95 dark:bg-gray-900/95 backdrop-blur-sm"
                    >
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
                class="mx-4 mb-3 rounded-lg border border-red-200/60 dark:border-red-500/20 bg-red-50/80 dark:bg-red-500/[0.07] overflow-hidden"
              >
                <div
                  class="flex items-center justify-between px-3 py-2 border-b border-red-200/40 dark:border-red-500/15"
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
                  class="px-3 py-2.5 text-xs text-red-700 dark:text-red-300 whitespace-pre-wrap break-all font-mono leading-relaxed m-0 max-h-48 overflow-y-auto"
                  >{{ job.errorDetail }}</pre
                >
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
/* Filters toolbar: side by side on desktop, two scrollable rows on mobile */
.filters-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
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

/* Job row: single flex line on desktop, wraps into two lines on mobile */
.job-row {
  display: flex;
  align-items: center;
  gap: 1rem;
}

@media (max-width: 639px) {
  .job-row {
    flex-wrap: wrap;
    gap: 0.5rem;
    row-gap: 0.5rem;
  }

  /* Line 1: status chip + progress bar (progress takes remaining space) */
  .job-status {
    order: 1;
    flex-shrink: 0;
  }

  .job-progress {
    order: 2;
    flex: 1;
    min-width: 0;
    width: auto !important; /* override the w-28 fixed width */
  }

  /* Actions stay on line 1, pushed to the right */
  .job-actions {
    order: 3;
    margin-left: auto;
  }

  /* Line 2: rungs + retry + error toggle — full width, smaller meta */
  .job-rungs {
    order: 4;
    width: 100%;
    flex: none;
  }

  .job-retry {
    order: 5;
  }

  /* Date is low-value on mobile — hide it */
  .job-date {
    display: none;
  }

  .job-error-toggle {
    order: 6;
    margin-left: auto;
  }
}
</style>
