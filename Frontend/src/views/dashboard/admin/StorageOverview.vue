<template>
  <div class="flex flex-col h-full w-full flex-1">
    <div class="flex w-full gap-3 px-6 py-4 items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-50">
          <UIcon name="mdi:server" class="w-4 h-4" />
        </div>
        <div>
          <h1 class="text-lg font-semibold tracking-tight">Storage</h1>
          <p class="text-xs text-muted">Server capacity and assigned quotas</p>
        </div>
      </div>
      <UButton variant="ghost" size="sm" @click="() => refreshAll()">
        <UIcon name="mdi:refresh" class="w-3.5 h-3.5 mr-1.5" />
        Refresh
      </UButton>
    </div>

    <div class="flex-1 overflow-auto">
      <div class="max-w-7xl mx-auto px-6 py-8 space-y-8">
        <div v-if="isLoading" class="flex items-center justify-center py-20 opacity-80">
          <UIcon name="mdi:loading" class="w-6 h-6 animate-spin" />
        </div>

        <div v-else-if="error" class="text-center py-16 opacity-90 space-y-2">
          <UIcon name="mdi:alert-circle-outline" class="w-10 h-10 mx-auto" />
          <p class="text-sm">Failed to load storage overview</p>
        </div>

        <template v-else-if="data">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest text-muted font-medium">
                    Garage capacity
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(data.capacity.garageCapacityBytes) }}
                  </p>
                  <p class="text-xs text-muted">
                    {{ data.capacity.garageNodes.length }} assigned
                    {{ data.capacity.garageNodes.length === 1 ? "node" : "nodes" }}
                  </p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:server" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest text-muted font-medium">
                    Server disk
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(data.capacity.dataAvailableBytes) }}
                  </p>
                  <p class="text-xs text-muted">
                    free of {{ formatBytes(data.capacity.dataTotalBytes) }} reported by Garage
                  </p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:harddisk" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest text-muted font-medium">
                    Assigned quotas
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(data.totalAssignedQuotaBytes) }}
                  </p>
                  <p class="text-xs text-muted">
                    across {{ data.totalUsers }}
                    {{ data.totalUsers === 1 ? "account" : "accounts" }} ·
                    {{ assignedPercentLabel }} of capacity
                  </p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:archive-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>
          </div>

          <div v-if="chartsLoading" class="flex items-center justify-center py-20 opacity-80">
            <UIcon name="mdi:loading" class="w-6 h-6 animate-spin" />
          </div>

          <div v-else-if="chartsError" class="text-center py-16 opacity-90 space-y-2">
            <UIcon name="mdi:alert-circle-outline" class="w-10 h-10 mx-auto" />
            <p class="text-sm">Failed to load storage breakdown</p>
          </div>

          <template v-else>
            <StorageBreakdownDiagram
              :labels="splitLabels"
              :data="splitSizes"
              :formatted-size="splitFormatted"
              subtitle="Server-wide usage by artifact type"
            />

            <UserStorageRankingChart :users="rankingData ?? []" />
          </template>

          <UCard :ui="{ body: 'p-0 sm:p-0', header: 'px-6 pt-6 pb-4' }">
            <template #header>
              <div class="flex items-center gap-3">
                <div class="p-2 rounded-lg border border-dashed opacity-60">
                  <UIcon name="mdi:server-network" class="w-4 h-4" />
                </div>
                <div>
                  <h3 class="font-semibold tracking-tight">Garage nodes</h3>
                  <p class="text-xs text-muted mt-0.5">Assigned role capacity per layout node</p>
                </div>
              </div>
            </template>
            <div
              v-if="data.capacity.garageNodes.length === 0"
              class="flex flex-col items-center justify-center gap-4 py-12 px-6 text-center"
            >
              <UIcon name="mdi:server" class="w-7 h-7 opacity-20" />
              <div class="space-y-1 max-w-xs">
                <p class="text-sm font-medium text-muted">No layout nodes reported</p>
                <p class="text-xs text-muted leading-relaxed">
                  The Garage metrics scrape did not include any node capacity labels.
                </p>
              </div>
            </div>

            <div v-else class="divide-y divide-dashed opacity-divide">
              <div
                v-for="node in data.capacity.garageNodes"
                :key="node.nodeId"
                class="flex items-center gap-3 px-6 py-3"
              >
                <span class="text-sm font-mono truncate flex-1" :title="node.nodeId">
                  {{ node.nodeId }}
                </span>
                <span class="text-xs tabular-nums text-muted">
                  {{ formatBytes(node.roleCapacityBytes) }}
                </span>
                <UBadge :color="node.connected ? 'success' : 'error'" variant="subtle" size="sm">
                  {{ node.connected ? "Connected" : "Disconnected" }}
                </UBadge>
              </div>
            </div>
          </UCard>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import UserStorageRankingChart from "@/components/dashboard/admin/UserStorageRankingChart.vue";
import StorageBreakdownDiagram from "@/components/dashboard/metrics/StorageBreakdownChart.vue";
import { adminStorageOverview, storageSplit, userStorageRanking } from "@/queries/status";
import { formatBytes } from "@/utils/size.utils";

const { data, isLoading, error, refresh } = useQuery(adminStorageOverview);
const {
  data: splitData,
  isLoading: splitLoading,
  error: splitError,
  refresh: refreshSplit,
} = useQuery(storageSplit);
const {
  data: rankingData,
  isLoading: rankingLoading,
  error: rankingError,
  refresh: refreshRanking,
} = useQuery(userStorageRanking, () => 10);

const refreshAll = () => {
  refresh();
  refreshSplit();
  refreshRanking();
};

const assignedPercentLabel = computed(() => {
  const capacity = data.value?.capacity.garageCapacityBytes ?? 0;
  if (capacity <= 0 || !data.value) return "—";
  return `${Math.round((data.value.totalAssignedQuotaBytes / capacity) * 100)}%`;
});

const splitLabels = ["Files", "Previews", "Transcoded", "Trash"];

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

const chartsLoading = computed(() => splitLoading.value || rankingLoading.value);
const chartsError = computed(() => splitError.value ?? rankingError.value ?? null);
</script>
