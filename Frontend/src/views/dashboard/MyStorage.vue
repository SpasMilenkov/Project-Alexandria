<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Header -->
    <div
      class="flex w-full gap-2 p-4 border-b border-gray-200 dark:border-gray-800 items-center justify-between"
    >
      <div class="flex items-center gap-2">
        <UIcon name="mdi:chart-pie" class="w-6 h-6" />
        <h1 class="text-2xl font-bold">Storage Management</h1>
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
        <UIcon name="mdi:refresh" class="w-4 h-4 mr-2" />
        Refresh
      </UButton>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto p-4">
      <div class="max-w-7xl mx-auto space-y-6">
        <!-- Storage Overview Widget -->
        <StorageInfoWidget :defaultState="true" />

        <StorageBreakdownDiagram
          v-if="sizeByCategory"
          :labels="sizeByCategory.categories"
          :data="sizeByCategory.size"
          :colors="categoryColors"
          :formatted-size="sizeByCategory.formattedSize"
        />

        <!-- Loading State -->
        <div
          v-if="myStorageIsLoading"
          class="flex items-center justify-center py-12"
        >
          <UIcon name="mdi:loading" class="w-8 h-8 animate-spin opacity-50" />
        </div>

        <!-- Error State -->
        <div v-else-if="myStorageError" class="text-center py-12">
          <UIcon
            name="mdi:alert-circle"
            class="w-12 h-12 mx-auto mb-4 opacity-50"
          />
          <p class="text-sm opacity-70">Failed to load storage breakdown</p>
        </div>

        <!-- Trash & Old Files Section -->
        <div v-else-if="myStorageData" class="space-y-6">
          <!-- Trash Size Card -->
          <UCard>
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div class="p-3 rounded-lg border">
                  <UIcon name="mdi:delete" class="w-6 h-6" />
                </div>
                <div>
                  <h3 class="font-semibold">Trash</h3>
                  <p class="text-sm opacity-70">File size in trash bin</p>
                </div>
              </div>
              <div class="text-right">
                <p class="text-2xl font-bold">
                  {{ formatBytes(myStorageData.trashSize) }}
                </p>
                <p class="text-xs opacity-70">Total size</p>
              </div>
            </div>
          </UCard>

          <!-- Old Files Section -->
          <div v-if="myStorageData.oldFiles.length > 0">
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-2">
                <UIcon name="mdi:clock-outline" class="w-5 h-5 opacity-70" />
                <h3 class="font-semibold opacity-70">Old Files</h3>
                <UBadge color="gray" variant="subtle">
                  {{ myStorageData.oldFiles.length }}
                </UBadge>
              </div>
            </div>

            <div class="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
              <UCard
                v-for="file in myStorageData.oldFiles"
                :key="file.id"
                class="hover:border-primary transition-colors cursor-pointer"
              >
                <div class="flex items-start gap-3">
                  <div class="p-2 rounded border shrink-0">
                    <UIcon :name="getFileIcon(file.fileName)" class="w-5 h-5" />
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="font-medium truncate" :title="file.fileName">
                      {{ file.fileName }}
                    </p>
                    <div class="flex items-center gap-2 mt-1">
                      <p class="text-xs opacity-70">
                        {{ getFileTypeReadable(file.mimeType) }}
                      </p>
                      <UBadge
                        v-if="file.hasPreview"
                        color="primary"
                        variant="subtle"
                        size="xs"
                      >
                        Preview
                      </UBadge>
                    </div>
                  </div>
                </div>
              </UCard>
            </div>
          </div>

          <!-- Empty State for Old Files -->
          <UCard v-else>
            <div class="text-center py-8">
              <UIcon
                name="mdi:check-circle"
                class="w-12 h-12 mx-auto mb-3 opacity-50"
              />
              <p class="font-medium">No old files</p>
              <p class="text-sm opacity-70 mt-1">
                All your files are up to date
              </p>
            </div>
          </UCard>
        </div>
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

const categoryColors = [
  "rgb(255, 99, 132)", // Soft Red
  "rgb(54, 162, 235)", // Blue
  "rgb(255, 205, 86)", // Yellow
  "rgb(75, 192, 192)", // Teal
  "rgb(153, 102, 255)", // Purple
  "rgb(255, 159, 64)", // Orange
  "rgb(199, 199, 199)", // Light Gray
  "rgb(83, 102, 255)", // Indigo
  "rgb(255, 99, 255)", // Pink
  "rgb(99, 255, 132)", // Mint Green
  "rgb(255, 140, 0)", // Dark Orange
  "rgb(0, 204, 150)", // Emerald
  "rgb(120, 120, 120)", // Neutral Gray
  "rgb(180, 70, 200)", // Violet
];

const sizeByCategory = computed(() => {
  if (myStorageData.value)
    return groupMimeSizeRecord(myStorageData.value.sizeByType);

  return null;
});

//TODO:
// Add proper grouping by mimetype (since mimetypes vary I should do the grouping, I might have a helper for that)
// Add proper spread in the pie diagram
// Add an event for to properly expand tree menu items when the proper category is selected on the pie chart
// Add better display for old files
// Figure out why the old files check picks up recent files
// Find better colors for the diagram
// Add a query that fetches the full file item if the user decides to click on that
// Add a Trash can page where the user can see all the deleted files
// Add a query that only selects files that have been deleted (it should be paginated and filtered, I don't have such thing on the backend yet (or maybe I do, I can reuse the search query
// and add the deleted only flag
</script>
