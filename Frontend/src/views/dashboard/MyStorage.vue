<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-hard-drive"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Storage</h1>
        <UBadge v-if="usageBadge" color="neutral" variant="subtle" size="sm">
          {{ usageBadge }}
        </UBadge>
      </div>

      <UTooltip text="Refresh">
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          icon="i-lucide-refresh-cw"
          square
          class="justify-center"
          aria-label="Refresh storage"
          :loading="isFetching"
          @click="refresh"
        />
      </UTooltip>
    </header>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div v-if="isPending" class="flex justify-center py-24">
        <BlockSpinner :size="24" aria-label="Loading storage" class="text-gray-500" />
      </div>

      <div
        v-else-if="visibleError || !data"
        class="flex flex-col items-center gap-3 rounded-[14px] border border-gray-200/70 px-4 py-16 text-center frosted-glass glass-surface dark:border-gray-700/70"
      >
        <UIcon name="i-lucide-triangle-alert" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
          Couldn't load storage data
        </p>
        <UButton color="neutral" variant="outline" size="sm" @click="refresh">Try again</UButton>
      </div>

      <div v-else class="flex flex-col gap-3">
        <StorageUsagePanel :data="data" />

        <StorageBreakdownChart
          :labels="sizeByCategory.categories"
          :data="sizeByCategory.size"
          :formatted-size="sizeByCategory.formattedSize"
          class="rounded-[14px] border border-gray-200/70 ring-0 frosted-glass glass-surface dark:border-gray-700/70"
        />

        <PreviewArtifactsPanel />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import StorageBreakdownChart from "@/components/dashboard/metrics/StorageBreakdownChart.vue";
import PreviewArtifactsPanel from "@/components/dashboard/storage/PreviewArtifactsPanel.vue";
import StorageUsagePanel from "@/components/dashboard/storage/StorageUsagePanel.vue";
import { STATUS_QUERY_KEYS, myStorage } from "@/queries/status";
import { groupMimeSizeRecord } from "@/utils/files/mimetype";

const queryCache = useQueryCache();

const { data, error, isPending, isLoading: isFetching } = useQuery(myStorage);

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const sizeByCategory = computed(() => groupMimeSizeRecord(data.value?.sizeByType ?? {}));

const usageBadge = computed(() => {
  const usage = data.value;

  if (!usage || usage.quotaBytes <= 0) return "";

  return `${Math.round((usage.usedBytes / usage.quotaBytes) * 100)}% used`;
});

// Refreshes the summary and the preview list together; both live under the status key.
const refresh = () => queryCache.invalidateQueries({ key: STATUS_QUERY_KEYS.root });
</script>
