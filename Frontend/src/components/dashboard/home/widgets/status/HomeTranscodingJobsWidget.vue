<script setup lang="ts">
import { useEventListener, useIntervalFn } from "@vueuse/core";
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";

import type { TranspilationJobResponse } from "@/api/streaming";
import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useHomeWidgetRows } from "@/composables/useHomeWidgetRows";
import { JobStatus } from "@/enums/job-status";
import { stopTranspilationJob as updateTranspilationJob } from "@/mutations/streaming";
import { homeTranscodingJobs } from "@/queries/home-media";
import { useHomeStore } from "@/stores/home";

defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const toast = useAppToast();
const list = ref<HTMLElement | null>(null);
const rowCount = useHomeWidgetRows(list, () => 56);
const retrying = ref<string | null>(null);
const mutation = updateTranspilationJob();

const query = useHomeWidgetQuery(
  () => homeTranscodingJobs({ userId: store.ownerId ?? "", epoch: store.ownerEpoch }),
  (preview) => ({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, value: preview.jobs }),
);

const pages = computed(() => {
  const response = query.data.value;

  if (response?.userId !== store.ownerId || response.epoch !== store.ownerEpoch) return null;

  return response.value;
});

const jobs = computed(() => {
  if (!pages.value) return [];

  return Array.from({ length: 6 }, (_, index) =>
    pages.value!.flatMap((page) => (page.items[index] ? [page.items[index]!] : [])),
  ).flat();
});
const visibleJobs = computed(() => jobs.value.slice(0, rowCount.value));
const total = computed(() => pages.value?.reduce((sum, page) => sum + page.totalCount, 0) ?? 0);
const authPending = computed(() => isAuthError(query.error.value));

const statusLabel = (status: JobStatus) => {
  if (status === JobStatus.Processing) return "Encoding";
  if (status === JobStatus.Queued) return "Queued";

  return "Failed";
};

const retry = async (job: TranspilationJobResponse) => {
  if (store.isEditing || retrying.value || job.status !== JobStatus.Failed || !store.ownerId)
    return;

  const epoch = store.ownerEpoch;
  retrying.value = job.id;

  try {
    await mutation.mutateAsync({ jobId: job.id, status: JobStatus.Queued });
  } catch {
    if (epoch === store.ownerEpoch)
      toast.error("Could not retry this job", "Try again from your transcoding jobs.");
  } finally {
    retrying.value = null;
  }
};

const refresh = () => {
  if (store.ownerId && !store.isEditing && !document.hidden) void query.refresh();
};

useIntervalFn(refresh, 30_000);
useEventListener(document, "visibilitychange", refresh);
</script>

<template>
  <div
    class="transcoding-widget flex min-h-0 flex-1 flex-col gap-2"
    :class="{ 'pointer-events-none': store.isEditing }"
  >
    <HomeWidgetDataState
      :loading="authPending || (!pages && !query.error.value)"
      :failed="Boolean(query.error.value) && !authPending"
      @retry="query.refresh()"
    >
      <div
        v-if="!total"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon
          name="i-heroicons-check-circle"
          class="size-8 shrink-0 text-gray-500 dark:text-gray-500"
        />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Nothing to transcode</p>
        <p class="jobs-extra text-xs text-gray-600 dark:text-gray-400">
          Your encoding queue is clear.
        </p>
        <UButton
          class="jobs-extra"
          to="/streaming/jobs"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="store.isEditing"
          >View history</UButton
        >
      </div>

      <template v-else>
        <p class="jobs-extra text-xs text-gray-600 dark:text-gray-400">
          {{ pages?.[0]?.totalCount }} running · {{ pages?.[1]?.totalCount }} queued ·
          {{ pages?.[2]?.totalCount }} failed
        </p>

        <div ref="list" class="min-h-0 flex-1 overflow-hidden">
          <div
            v-for="job in visibleJobs"
            :key="job.id"
            class="job-row flex h-14 min-w-0 items-center gap-2 border-b border-gray-200/50 last:border-0 dark:border-gray-700/50"
          >
            <UIcon
              :name="job.isVideo ? 'i-heroicons-video-camera' : 'i-heroicons-musical-note'"
              class="size-5 shrink-0 text-gray-500 dark:text-gray-500"
            />
            <div class="min-w-0 flex-1">
              <p
                class="truncate text-sm font-medium text-gray-900 dark:text-gray-100"
                :title="job.fileName"
              >
                {{ job.fileName || "Media file" }}
              </p>
              <p class="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
                <UIcon
                  v-if="job.status === JobStatus.Processing"
                  name="i-mdi-loading"
                  class="size-3.5 shrink-0 animate-spin"
                />
                <span
                  :class="{ 'text-red-700 dark:text-red-400': job.status === JobStatus.Failed }"
                  >{{ statusLabel(job.status) }}</span
                >
                <span v-if="job.retryCount" class="job-attempt truncate"
                  >{{ job.retryCount }} retries</span
                >
              </p>
            </div>

            <UButton
              v-if="job.status === JobStatus.Failed"
              icon="i-heroicons-arrow-path"
              color="neutral"
              variant="ghost"
              size="xs"
              square
              class="justify-center"
              :aria-label="`Retry ${job.fileName || 'media file'}`"
              :loading="retrying === job.id"
              :disabled="store.isEditing || Boolean(retrying)"
              @click="retry(job)"
            />
          </div>
        </div>

        <RouterLink
          class="jobs-extra text-xs text-gray-600 hover:underline dark:text-gray-400"
          to="/streaming/jobs"
          :tabindex="store.isEditing ? -1 : undefined"
          >View all jobs</RouterLink
        >
      </template>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
@container home-widget (max-height: 200px) {
  .jobs-extra {
    display: none;
  }
}

@container home-widget (max-width: 240px) {
  .job-attempt {
    display: none;
  }
}
</style>
