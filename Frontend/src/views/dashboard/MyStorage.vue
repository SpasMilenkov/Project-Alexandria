<template>
  <div class="flex flex-col h-full w-full flex-1 ">
    <!-- Header -->
    <div
      class="flex w-full gap-3 px-6 py-4 border-b items-center justify-between"
    >
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-50">
          <UIcon name="mdi:chart-pie" class="w-4 h-4" />
        </div>
        <div>
          <h1 class="text-lg font-semibold tracking-tight">Storage</h1>
          <p class="text-xs opacity-90">Manage and inspect your usage</p>
        </div>
      </div>
      <UButton
        variant="ghost"
        size="sm"
        @click="
          () => {
            refreshStorageData();
            refreshMyStorage();
          }
        "
      >
        <UIcon name="mdi:refresh" class="w-3.5 h-3.5 mr-1.5" />
        Refresh
      </UButton>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto">
      <div class="max-w-7xl mx-auto px-6 py-8 space-y-8">
        <!-- Storage Overview Widget -->
        <StorageInfoWidget :defaultState="true" />

        <!-- Breakdown Chart -->
        <StorageBreakdownDiagram
          v-if="sizeByCategory"
          :labels="sizeByCategory.categories"
          :data="sizeByCategory.size"
          :formatted-size="sizeByCategory.formattedSize"
        />

        <!-- Loading -->
        <div
          v-if="myStorageIsLoading"
          class="flex items-center justify-center py-20 opacity-80"
        >
          <UIcon name="mdi:loading" class="w-6 h-6 animate-spin" />
        </div>

        <!-- Error -->
        <div
          v-else-if="myStorageError"
          class="text-center py-16 opacity-90 space-y-2"
        >
          <UIcon name="mdi:alert-circle-outline" class="w-10 h-10 mx-auto" />
          <p class="text-sm">Failed to load storage data</p>
        </div>

        <template v-else-if="myStorageData">
          <!-- Stat row: Trash + quick stats -->
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <UCard :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p
                    class="text-xs uppercase tracking-widest opacity-90 font-medium"
                  >
                    Trash
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(myStorageData.trashSize) }}
                  </p>
                  <p class="text-xs opacity-90">Awaiting cleanup</p>
                </div>
                <div
                  class="p-2 rounded-lg border border-dashed opacity-90 mt-0.5"
                >
                  <UIcon name="mdi:delete-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p
                    class="text-xs uppercase tracking-widest opacity-90 font-medium"
                  >
                    Old Files
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ myStorageData.oldFiles.length }}
                  </p>
                  <p class="text-xs opacity-90">Not accessed recently</p>
                </div>
                <div
                  class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5"
                >
                  <UIcon name="mdi:clock-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p
                    class="text-xs uppercase tracking-widest opacity-90 font-medium"
                  >
                    File Types
                  </p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ sizeByCategory?.categories.length ?? "—" }}
                  </p>
                  <p class="text-xs opacity-90">Distinct categories</p>
                </div>
                <div
                  class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5"
                >
                  <UIcon name="mdi:shape-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>
          </div>

          <!-- Old Files Section -->
          <div v-if="myStorageData.oldFiles.length > 0">
            <div class="flex items-center gap-3 mb-5">
              <div class="h-px flex-1 border-t border-dashed opacity-20" />
              <div
                class="flex items-center gap-2 text-xs opacity-90 uppercase tracking-widest font-medium"
              >
                <UIcon name="mdi:clock-outline" class="w-3.5 h-3.5" />
                Old Files
                <span class="normal-case tracking-normal font-normal opacity-70"
                  >({{ myStorageData.oldFiles.length }})</span
                >
              </div>
              <div class="h-px flex-1 border-t border-dashed opacity-20" />
            </div>

            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <div
                v-for="file in myStorageData.oldFiles"
                :key="file.id"
                class="group flex items-start gap-3 p-4 rounded-lg border border-dashed hover:border-solid hover:border-primary/40 transition-all cursor-pointer bg-black/1 dark:bg-white/1 hover:bg-black/3 dark:hover:bg-white/3"
              >
                <div
                  class="p-2 rounded-md border opacity-50 group-hover:opacity-80 transition-opacity shrink-0"
                >
                  <UIcon :name="getFileIcon(file.fileName)" class="w-4 h-4" />
                </div>
                <div class="flex-1 min-w-0 space-y-1">
                  <p
                    class="text-sm font-medium truncate leading-snug"
                    :title="file.fileName"
                  >
                    {{ file.fileName }}
                  </p>
                  <div class="flex items-center gap-1.5 flex-wrap">
                    <span class="text-xs opacity-90">{{
                      getFileTypeReadable(file.mimeType)
                    }}</span>
                    <span
                      v-if="file.hasPreview"
                      class="inline-flex items-center text-[10px] px-1.5 py-0.5 rounded border opacity-50 font-medium"
                    >
                      Preview
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty state for old files -->
          <div v-else class="text-center py-16 space-y-3 opacity-90">
            <UIcon name="mdi:check-circle-outline" class="w-10 h-10 mx-auto" />
            <div>
              <p class="font-medium text-sm">No old files</p>
              <p class="text-xs mt-0.5">Everything looks tidy</p>
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { storageInfo, myStorage } from "@/queries/status";
import StorageInfoWidget from "@/components/dashboard/metrics/StorageInfoWidget.vue";
import StorageBreakdownDiagram from "@/components/dashboard/metrics/StorageBreakdownChart.vue";
import { groupMimeSizeRecord } from "@/utils/mimetype.utils";
import { formatBytes } from "@/utils/size.utils";
import { computed } from "vue";
import { getFileIcon } from "@/utils/icon.utils";
import { getFileTypeReadable } from "@/utils/mimetype.utils";

const { refresh: refreshStorageData } = useQuery(storageInfo);
const {
  data: myStorageData,
  isLoading: myStorageIsLoading,
  refresh: refreshMyStorage,
  error: myStorageError,
} = useQuery(myStorage);

const sizeByCategory = computed(() => {
  if (myStorageData.value)
    return groupMimeSizeRecord(myStorageData.value.sizeByType);
  return null;
});
</script>
