<script setup lang="ts">
import { computed } from "vue";
import { useActivityStore } from "@/stores/activity";
import { SortDirection } from "@/enums/SortDirection";
import { OperationType, EntityType } from "@/api/activity";
import { useAuthStore } from "@/stores/auth";
import type { TimelineItem } from "@nuxt/ui";
import { useQuery } from "@pinia/colada";
import { personalPaginated } from "@/queries/activities";

const activityStore = useActivityStore();
const authStore = useAuthStore();

const { data, refresh, isLoading, error } = useQuery(personalPaginated, () => ({
  page: activityStore.page,
  pageSize: activityStore.pageSize,
  sortBy: "timestamp",
  sortDirection: SortDirection.Desc,
  userId: authStore.user?.id,
}));

const items = computed((): TimelineItem[] =>
  !data.value
    ? []
    : data.value?.items.map((log) => ({
        date: new Date(log.timestamp).toLocaleDateString(),
        value: log.entityId + log.timestamp,
        icon: getIconForOperation(log.operationType, log.entityTYpe),
        title:
          log.description || getDefaultTitle(log.operationType, log.entityTYpe),
        operationType: log.operationType,
        entityType: log.entityTYpe,
      })),
);

const getIconForOperation = (opType: OperationType, entityType: EntityType) => {
  if (entityType === EntityType.Directory) {
    return "i-lucide-folder";
  } else if (entityType === EntityType.File) {
    return "i-lucide-file";
  } else if (entityType === EntityType.Tag) {
    return "i-lucide-tag";
  } else if (entityType === EntityType.User) {
    return "i-lucide-user";
  }

  switch (opType) {
    case OperationType.Create:
      return "i-lucide-plus-circle";
    case OperationType.Update:
      return "i-lucide-edit";
    case OperationType.Delete:
      return "i-lucide-trash-2";
    case OperationType.Login:
      return "i-lucide-log-in";
    case OperationType.Logout:
      return "i-lucide-log-out";
    default:
      return "i-lucide-eye";
  }
};

const getDefaultTitle = (opType: OperationType, entityType: EntityType) => {
  const entityName = EntityType[entityType].toLowerCase();
  const operation = OperationType[opType].toLowerCase();
  return `${operation} ${entityName}`;
};

const changePage = (pageNumber: number) => {
  activityStore.page = pageNumber;
  refresh();
};
</script>

<template>
  <div class="min-h-screen p-6 sm:p-8">
    <div class="max-w-4xl mx-auto">
      <!-- Header Section -->
      <div class="mb-8">
        <h1 class="text-3xl font-bold mb-2">Activity History</h1>
      </div>

      <!-- Error Alert -->
      <UAlert
        v-if="error"
        color="error"
        variant="subtle"
        title="Error loading activity"
        :description="error.message"
        class="mb-6"
      />

      <!-- Stats Card -->
      <UCard class="mb-6">
        <div class="flex items-center justify-between">
          <div>
            <p class="text-sm opacity-70 mb-1">Total Activities</p>
            <p class="text-2xl font-semibold">
              {{ isLoading ? "..." : data?.totalCount }}
            </p>
          </div>
          <UIcon name="i-lucide-activity" class="w-8 h-8 opacity-50" />
        </div>
      </UCard>

      <!-- Loading Skeleton State -->
      <UCard v-if="isLoading">
        <template #header>
          <div class="flex items-center justify-between">
            <USkeleton class="h-7 w-40" />
            <USkeleton class="h-5 w-12 rounded-full" />
          </div>
        </template>

        <!-- Timeline Skeleton -->
        <div class="space-y-6">
          <div class="flex gap-4">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
              <div class="w-px h-full bg-gray-200 dark:bg-gray-800 mt-2" />
            </div>
            <div class="flex-1 pb-6">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-64 mb-1" />
            </div>
          </div>

          <div class="flex gap-4">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
              <div class="w-px h-full bg-gray-200 dark:bg-gray-800 mt-2" />
            </div>
            <div class="flex-1 pb-6">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-72 mb-1" />
            </div>
          </div>

          <div class="flex gap-4">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
              <div class="w-px h-full bg-gray-200 dark:bg-gray-800 mt-2" />
            </div>
            <div class="flex-1 pb-6">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-56 mb-1" />
            </div>
          </div>

          <div class="flex gap-4">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
              <div class="w-px h-full bg-gray-200 dark:bg-gray-800 mt-2" />
            </div>
            <div class="flex-1 pb-6">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-64 mb-1" />
            </div>
          </div>

          <div class="flex gap-4">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
            </div>
            <div class="flex-1">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-48 mb-1" />
            </div>
          </div>
        </div>

        <!-- Pagination Skeleton -->
        <div
          class="flex justify-center items-center gap-2 mt-6 pt-6 border-t border-gray-200 dark:border-gray-800"
        >
          <USkeleton class="h-9 w-9 rounded-md" />
          <USkeleton class="h-9 w-9 rounded-md" />
          <USkeleton class="h-9 w-9 rounded-md" />
          <USkeleton class="h-9 w-9 rounded-md" />
          <USkeleton class="h-9 w-9 rounded-md" />
        </div>
      </UCard>

      <!-- Timeline Section -->
      <UCard v-else-if="data">
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-xl font-semibold">Recent Activity</h2>
            <UBadge variant="subtle" color="info">{{ data.totalCount }}</UBadge>
          </div>
        </template>

        <UTimeline :items="items" />
        <UPagination
          v-model:page="activityStore.page"
          :total="data.totalCount"
          @update:page="changePage"
        />
      </UCard>

      <!-- Empty State -->
      <UCard v-else>
        <div class="text-center py-12">
          <UIcon
            name="i-lucide-folder-open"
            class="w-12 h-12 mx-auto mb-4 opacity-50"
          />
          <h3 class="text-lg font-semibold mb-2">No activity history yet</h3>
          <p class="text-sm opacity-70">
            Your activity will appear here as you interact with the system
          </p>
        </div>
      </UCard>
    </div>
  </div>
</template>
