<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { AdminJob } from "@/api/jobs";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { JobStatus, type JobType } from "@/enums";
import { useRequeueFailedJobs, useRequeueJob } from "@/mutations/jobs";
import { adminJobs } from "@/queries/jobs";
import { formatRelativeShort } from "@/utils/date-formatters";

const { type, title } = defineProps<{
  type: JobType;
  title: string;
}>();

const PAGE_SIZE = 10;

const STATUS_VIEWS = [
  { label: "Failed", value: JobStatus.Failed },
  { label: "Processing", value: JobStatus.Processing },
] as const;

const statusView = ref<JobStatus>(JobStatus.Failed);
const page = ref(1);

watch(
  [statusView, () => type],
  () => {
    page.value = 1;
  },
  { flush: "sync" },
);

const { data, error, isLoading: isFetching, refetch } = useQuery(() =>
  adminJobs({ page: page.value, pageSize: PAGE_SIZE, status: statusView.value, type }),
);

useLiveRefresh(refetch, 60_000);

// Previous rows stay visible during background refetches; only a view without data spins.
const isLoading = computed(() => data.value === undefined && isFetching.value);

const jobs = computed<AdminJob[]>(() => data.value?.items ?? []);
const totalCount = computed(() => data.value?.totalCount ?? 0);
const totalPages = computed(() => Math.max(data.value?.totalPages ?? 1, 1));

const isFailedView = computed(() => statusView.value === JobStatus.Failed);

const emptyHeading = computed(() => {
  if (isFailedView.value) return "No failed jobs";
  return "Nothing processing";
});

const emptyDescription = computed(() => {
  if (isFailedView.value) return "Failed jobs that can be retried will appear here.";
  return "Jobs currently being worked on appear here; stalled ones can be requeued.";
});

const ageLabel = (job: AdminJob): string => {
  const age = formatRelativeShort(job.completedAt ?? job.updatedAt ?? job.createdAt);
  if (age === "just now") return age;
  return `${age} ago`;
};

const errorLabel = (job: AdminJob): string => {
  if (job.errorDetail) return job.errorDetail;
  if (job.status === JobStatus.Processing) return "In progress";
  return "No error recorded";
};

const shortId = (id: string): string => id.slice(0, 8);

const toast = useAppToast();
const { mutateAsync: requeueJob } = useRequeueJob();
const { mutateAsync: requeueFailed, isLoading: isRequeueingAll } = useRequeueFailedJobs();

const pendingJobId = ref<string | null>(null);

const handleRequeue = async (job: AdminJob) => {
  pendingJobId.value = job.id;
  try {
    await requeueJob(job.id);
    toast.success("Job requeued");
  } catch (err) {
    toast.error("Could not requeue job", err);
  } finally {
    pendingJobId.value = null;
  }
};

const handleRequeueAll = async () => {
  try {
    const result = await requeueFailed(type);
    toast.success(`Requeued ${result.requeued} job${result.requeued === 1 ? "" : "s"}`);
  } catch (err) {
    toast.error("Could not requeue failed jobs", err);
  }
};

const prevPage = () => {
  if (page.value > 1) page.value--;
};

const nextPage = () => {
  if (page.value < totalPages.value) page.value++;
};
</script>

<template>
  <div
    class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
  >
    <div
      class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2 flex-wrap"
    >
      <UIcon name="i-mdi-restore" class="w-4 h-4 text-gray-400 dark:text-gray-500 shrink-0" />
      <span class="text-sm font-semibold text-gray-700 dark:text-gray-300">{{ title }}</span>
      <UBadge v-if="data" color="neutral" variant="subtle" size="sm">{{ totalCount }}</UBadge>

      <div class="ml-auto flex items-center gap-2">
        <div class="flex items-center rounded-lg border border-gray-200/70 dark:border-gray-700/70">
          <UButton
            v-for="view in STATUS_VIEWS"
            :key="view.value"
            size="xs"
            :color="statusView === view.value ? 'primary' : 'neutral'"
            :variant="statusView === view.value ? 'soft' : 'ghost'"
            @click="statusView = view.value"
          >
            {{ view.label }}
          </UButton>
        </div>

        <UButton
          v-if="isFailedView && totalCount > 0"
          size="xs"
          color="neutral"
          variant="outline"
          icon="i-mdi-restore"
          :loading="isRequeueingAll"
          @click="handleRequeueAll()"
        >
          Requeue all
        </UButton>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center py-14">
      <BlockSpinner :size="24" aria-label="Loading jobs" class="text-gray-400 dark:text-gray-500" />
    </div>

    <UAlert
      v-else-if="error && !data"
      color="error"
      variant="subtle"
      icon="i-mdi-alert-circle-outline"
      title="Failed to load jobs"
      :description="error.message"
      class="m-4"
    />

    <div v-else-if="jobs.length === 0" class="flex flex-col items-center text-center px-6 py-10">
      <UIcon name="i-mdi-check-circle-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <p class="mt-3 text-sm font-medium text-gray-900 dark:text-gray-100">{{ emptyHeading }}</p>
      <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">{{ emptyDescription }}</p>
    </div>

    <template v-else>
      <div class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
        <div v-for="job in jobs" :key="job.id" class="flex items-center gap-3 px-5 py-3">
          <span
            class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
            :class="isFailedView ? 'bg-red-500/10' : 'bg-gray-500/10'"
          >
            <UIcon
              :name="isFailedView ? 'i-mdi-alert-circle-outline' : 'i-mdi-history'"
              class="w-4 h-4"
              :class="isFailedView ? 'text-red-500' : 'text-gray-500 dark:text-gray-400'"
            />
          </span>

          <div class="min-w-0 flex-1">
            <p class="text-sm text-gray-900 dark:text-gray-100 truncate" :title="errorLabel(job)">
              {{ errorLabel(job) }}
            </p>
            <p class="text-xs text-gray-500 dark:text-gray-500 mt-0.5">
              {{ shortId(job.id) }} · {{ job.retryCount }}
              {{ job.retryCount === 1 ? "retry" : "retries" }}
            </p>
          </div>

          <span class="text-xs text-gray-500 dark:text-gray-500 shrink-0 tabular-nums">
            {{ ageLabel(job) }}
          </span>

          <UButton
            size="xs"
            color="neutral"
            variant="outline"
            :loading="pendingJobId === job.id"
            :disabled="pendingJobId !== null && pendingJobId !== job.id"
            @click="handleRequeue(job)"
          >
            Requeue
          </UButton>
        </div>
      </div>

      <div
        v-if="totalPages > 1"
        class="flex items-center justify-between gap-2 px-5 py-3 border-t border-gray-200/70 dark:border-gray-700/70"
      >
        <span class="text-xs text-gray-500 dark:text-gray-500">
          Page {{ page }} of {{ totalPages }}
        </span>
        <div class="flex items-center gap-2">
          <UButton
            size="xs"
            color="neutral"
            variant="ghost"
            :disabled="page <= 1"
            @click="prevPage()"
          >
            Previous
          </UButton>
          <UButton
            size="xs"
            color="neutral"
            variant="ghost"
            :disabled="page >= totalPages"
            @click="nextPage()"
          >
            Next
          </UButton>
        </div>
      </div>
    </template>
  </div>
</template>
