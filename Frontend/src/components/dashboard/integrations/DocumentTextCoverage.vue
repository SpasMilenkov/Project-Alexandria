<template>
  <div class="flex flex-col gap-3">
    <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <StatTile
        label="Searchable"
        icon="i-lucide-text-search"
        caption="documents with text"
        :loading="isPending"
      >
        {{ searchable.toLocaleString() }}
      </StatTile>
      <StatTile label="Waiting" caption="not read yet" :loading="isPending">
        {{ waiting.toLocaleString() }}
      </StatTile>
      <StatTile
        label="Failed"
        :caption="failedCaption"
        :loading="isPending"
        :tone="failed > 0 ? 'danger' : 'default'"
      >
        {{ failed.toLocaleString() }}
      </StatTile>
      <StatTile label="Can't be searched" caption="read, but no text" :loading="isPending">
        {{ unsearchable.toLocaleString() }}
      </StatTile>
    </div>

    <UAlert
      v-if="error && !data"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      title="Couldn't load document text coverage"
      :description="error.message"
    />

    <div v-else class="grid grid-cols-1 gap-3 lg:grid-cols-2">
      <IntegrationPanel title="Recent failures" flush>
        <template #actions>
          <UButton
            v-if="failed > 0"
            class="ml-auto"
            size="xs"
            color="neutral"
            variant="outline"
            icon="i-lucide-rotate-ccw"
            :loading="isRetrying"
            @click="handleRetry()"
          >
            Retry all
          </UButton>
        </template>

        <div v-if="isPending" class="flex items-center justify-center py-14">
          <BlockSpinner :size="24" aria-label="Loading failures" class="text-gray-500" />
        </div>

        <div
          v-else-if="failures.length === 0"
          class="flex flex-col items-center px-6 py-10 text-center"
        >
          <UIcon name="i-lucide-circle-check" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
          <p class="mt-3 text-sm font-medium text-gray-900 dark:text-gray-100">No failures</p>
          <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">
            Documents whose text couldn't be read appear here.
          </p>
        </div>

        <ul v-else class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
          <li
            v-for="failure in failures"
            :key="failure.contentObjectId"
            class="flex items-start gap-3 px-5 py-3"
            data-document-text-failure
          >
            <span
              class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-red-500/10"
            >
              <UIcon name="i-lucide-circle-alert" class="w-4 h-4 text-red-500" />
            </span>

            <div class="min-w-0 flex-1">
              <p
                class="truncate text-sm"
                :class="
                  failure.fileName
                    ? 'text-gray-900 dark:text-gray-100'
                    : 'italic text-gray-600 dark:text-gray-400'
                "
              >
                {{ failureTitle(failure) }}
              </p>
              <p
                class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400"
                :title="failure.errorDetail ?? undefined"
              >
                {{ failureDetailLabel(failure) }}
              </p>
              <p class="mt-0.5 text-xs text-gray-500 dark:text-gray-500">
                {{ failureMeta(failure) }}
              </p>
            </div>

            <span
              v-if="failure.lastAttemptAt"
              class="shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-500"
            >
              {{ formatTimeAgo(failure.lastAttemptAt, { style: "narrow" }) }}
            </span>
          </li>
        </ul>
      </IntegrationPanel>

      <IntegrationPanel title="Why some can't be searched" flush>
        <div v-if="isPending" class="flex items-center justify-center py-14">
          <BlockSpinner :size="24" aria-label="Loading reasons" class="text-gray-500" />
        </div>

        <div
          v-else-if="reasons.length === 0"
          class="flex flex-col items-center px-6 py-10 text-center"
        >
          <UIcon name="i-lucide-text-search" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
          <p class="mt-3 text-sm font-medium text-gray-900 dark:text-gray-100">
            Every document read so far has text
          </p>
          <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">
            Scans, locked files and empty documents would be counted here.
          </p>
        </div>

        <ul v-else class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
          <li
            v-for="reason in reasons"
            :key="reason.status"
            class="flex items-start gap-3 px-5 py-3"
            data-unsearchable-reason
          >
            <span
              class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gray-500/10"
            >
              <UIcon :name="reason.icon" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
            </span>

            <div class="min-w-0 flex-1">
              <p class="text-sm text-gray-900 dark:text-gray-100">{{ reason.label }}</p>
              <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
                {{ reason.description }}
              </p>
            </div>

            <span
              class="shrink-0 text-sm font-semibold tabular-nums text-gray-900 dark:text-gray-100"
            >
              {{ reason.count.toLocaleString() }}
            </span>
          </li>
        </ul>
      </IntegrationPanel>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { DocumentTextFailure } from "@/api/documentTextStats";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import StatTile from "@/components/dashboard/integrations/StatTile.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { DocumentTextStatus } from "@/enums";
import {
  failureAttemptLabel,
  failureDetailLabel,
  failureTitle,
  failureTypeLabel,
  statusCount,
  unsearchableReasons,
  unsearchableTotal,
} from "@/features/monitoring/document-text-coverage";
import { useRetryFailedDocumentText } from "@/mutations/documentTextStats";
import { documentTextOverview } from "@/queries/documentTextStats";
import { formatCount } from "@/utils/count";
import { formatTimeAgo } from "@/utils/date-formatters";

const { data, error, isPending, refetch } = useQuery(documentTextOverview());

// The sweep works through documents in the background, so the counts are polled.
useLiveRefresh(refetch, 60_000);

const counts = computed(() => data.value?.statusCounts ?? []);
const failures = computed(() => data.value?.recentFailures ?? []);

const searchable = computed(() => statusCount(counts.value, DocumentTextStatus.Extracted));
const waiting = computed(() => data.value?.waitingCount ?? 0);
const failed = computed(() => statusCount(counts.value, DocumentTextStatus.Failed));
const unsearchable = computed(() => unsearchableTotal(counts.value));
const reasons = computed(() => unsearchableReasons(counts.value));

const failedCaption = computed(() => {
  if (failed.value === 0) {
    return "none";
  }
  return `up to ${formatCount(data.value?.maxAttempts ?? 0, "try", "tries")} each`;
});

const failureMeta = (failure: DocumentTextFailure) => {
  const attempts = failureAttemptLabel(failure, data.value?.maxAttempts ?? 0);
  const type = failureTypeLabel(failure);
  return type ? `${type} · ${attempts}` : attempts;
};

const toast = useAppToast("previews");
const { mutateAsync: retryFailed, isLoading: isRetrying } = useRetryFailedDocumentText();

const handleRetry = async () => {
  try {
    const result = await retryFailed();
    if (result.reset === 0) {
      toast.info("No failed documents to retry");
      return;
    }

    toast.success(`Queued ${formatCount(result.reset, "document")} for another try`);
  } catch (err) {
    toast.error("Couldn't queue the failed documents", err, { retry: handleRetry });
  }
};
</script>
