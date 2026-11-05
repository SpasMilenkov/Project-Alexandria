<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-archive" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Storage</h1>
        <UBadge v-if="assignedBadge" color="neutral" variant="subtle" size="sm">
          {{ assignedBadge }}
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
          @click="refreshAll"
        />
      </UTooltip>
    </header>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div v-if="isPending" class="flex justify-center py-24">
        <BlockSpinner :size="24" aria-label="Loading storage overview" class="text-gray-500" />
      </div>

      <div
        v-else-if="visibleError || !data"
        class="flex flex-col items-center gap-3 rounded-[14px] border border-gray-200/70 px-4 py-16 text-center frosted-glass glass-surface dark:border-gray-700/70"
      >
        <UIcon name="i-lucide-triangle-alert" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
          Couldn't load the storage overview
        </p>
        <UButton color="neutral" variant="outline" size="sm" @click="refreshAll">Try again</UButton>
      </div>

      <div v-else class="flex flex-col gap-3">
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <StatTile label="Garage capacity" :caption="nodesCaption">
            {{ formatBytes(data.capacity.garageCapacityBytes) }}
          </StatTile>

          <StatTile
            label="Server disk free"
            :caption="`of ${formatBytes(data.capacity.dataTotalBytes)} reported by Garage`"
          >
            {{ formatBytes(data.capacity.dataAvailableBytes) }}
          </StatTile>

          <StatTile label="Assigned quotas" :caption="quotaCaption">
            {{ formatBytes(data.totalAssignedQuotaBytes) }}
          </StatTile>
        </div>

        <IntegrationPanel v-if="splitPending" title="Storage breakdown">
          <div class="flex h-56 items-center justify-center">
            <BlockSpinner :size="24" aria-label="Loading storage breakdown" class="text-gray-500" />
          </div>
        </IntegrationPanel>

        <UAlert
          v-else-if="visibleSplitError"
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Failed to load the storage breakdown"
          :description="visibleSplitError.message"
        />

        <StorageBreakdownChart
          v-else
          :labels="SPLIT_LABELS"
          :data="splitSizes"
          :formatted-size="splitFormatted"
          subtitle="Server-wide usage by artifact type"
          class="rounded-[14px] border border-gray-200/70 ring-0 frosted-glass glass-surface dark:border-gray-700/70"
        />

        <div class="grid grid-cols-1 items-start gap-3 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
          <UAlert
            v-if="visibleRankingError"
            color="error"
            variant="subtle"
            icon="i-lucide-alert-circle"
            title="Failed to load top consumers"
            :description="visibleRankingError.message"
          />

          <UserStorageRankingChart v-else :users="rankingData ?? []" :loading="rankingPending" />

          <IntegrationPanel title="Garage nodes" hint="Role capacity per layout node" flush>
            <div
              v-if="data.capacity.garageNodes.length === 0"
              class="flex flex-col items-center justify-center gap-2 px-6 py-12 text-center"
            >
              <UIcon
                name="i-lucide-hard-drive"
                class="h-12 w-12 text-gray-400 dark:text-gray-600"
              />
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                No layout nodes reported
              </p>
              <p class="max-w-xs text-xs text-gray-600 dark:text-gray-400">
                The Garage metrics scrape did not include any node capacity labels.
              </p>
            </div>

            <ul v-else class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
              <li
                v-for="node in data.capacity.garageNodes"
                :key="node.nodeId"
                class="flex items-center gap-3 px-4 py-3 sm:px-5"
              >
                <span
                  class="min-w-0 flex-1 truncate font-mono text-sm text-gray-900 dark:text-gray-100"
                  :title="node.nodeId"
                >
                  {{ node.nodeId }}
                </span>

                <span class="shrink-0 text-xs tabular-nums text-gray-600 dark:text-gray-400">
                  {{ formatBytes(node.roleCapacityBytes) }}
                </span>

                <UBadge
                  :color="node.connected ? 'success' : 'error'"
                  variant="subtle"
                  size="sm"
                  class="shrink-0"
                >
                  {{ connectionLabel(node.connected) }}
                </UBadge>
              </li>
            </ul>
          </IntegrationPanel>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import UserStorageRankingChart from "@/components/dashboard/admin/UserStorageRankingChart.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import StatTile from "@/components/dashboard/integrations/StatTile.vue";
import StorageBreakdownChart from "@/components/dashboard/metrics/StorageBreakdownChart.vue";
import { adminStorageOverview, storageSplit, userStorageRanking } from "@/queries/status";
import { formatBytes } from "@/utils/size";

const SPLIT_LABELS = ["Files", "Previews", "Transcoded", "Trash"];

const {
  data,
  error,
  isPending,
  isLoading: overviewFetching,
  refetch: refetchOverview,
} = useQuery(adminStorageOverview);

const {
  data: splitData,
  error: splitError,
  isPending: splitPending,
  isLoading: splitFetching,
  refetch: refetchSplit,
} = useQuery(storageSplit);

const {
  data: rankingData,
  error: rankingError,
  isPending: rankingPending,
  isLoading: rankingFetching,
  refetch: refetchRanking,
} = useQuery(() => userStorageRanking(10));

const isFetching = computed(
  () => overviewFetching.value || splitFetching.value || rankingFetching.value,
);

const refreshAll = () => {
  void refetchOverview();
  void refetchSplit();
  void refetchRanking();
};

const visibleOf = (err: Error | null): Error | null => {
  if (!err || isAuthError(err)) return null;

  return err;
};

const visibleError = computed(() => visibleOf(error.value));
const visibleSplitError = computed(() => visibleOf(splitError.value));
const visibleRankingError = computed(() => visibleOf(rankingError.value));

const plural = (count: number, singular: string, pluralForm: string): string => {
  if (count === 1) return `${count} ${singular}`;

  return `${count} ${pluralForm}`;
};

const assignedPercent = computed(() => {
  const capacity = data.value?.capacity.garageCapacityBytes ?? 0;

  if (!data.value || capacity <= 0) return null;

  return Math.round((data.value.totalAssignedQuotaBytes / capacity) * 100);
});

const assignedBadge = computed(() => {
  if (assignedPercent.value === null) return "";

  return `${assignedPercent.value}% of capacity assigned`;
});

const nodesCaption = computed(() =>
  plural(data.value?.capacity.garageNodes.length ?? 0, "assigned node", "assigned nodes"),
);

const quotaCaption = computed(
  () => `across ${plural(data.value?.totalUsers ?? 0, "account", "accounts")}`,
);

const connectionLabel = (connected: boolean): string => {
  if (connected) return "Connected";

  return "Disconnected";
};

const splitSizes = computed(() => {
  if (!splitData.value) return [];

  return [
    splitData.value.filesSize,
    splitData.value.previewsSize,
    splitData.value.transcodedSize,
    splitData.value.trashSize,
  ];
});

const splitFormatted = computed(() => splitSizes.value.map((bytes) => formatBytes(bytes)));
</script>
