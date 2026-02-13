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
  <div class="min-h-screen px-4 py-6 sm:px-6 lg:px-8 md:py-0">
    <div class="max-w-6xl mx-auto space-y-6 lg:space-y-8">
      <div
        class="flex flex-col gap-4 md:flex-row md:items-end md:justify-between"
      >
        <div>
          <h1 class="text-3xl font-bold mb-1">Activity History</h1>
          <p class="text-sm opacity-70">
            Review your recent actions across files, folders, tags and more.
          </p>
        </div>

        <UCard class="md:w-64 lg:w-72 shrink-0">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs uppercase tracking-wide opacity-70 mb-1">
                Total activities
              </p>
              <p class="text-2xl font-semibold leading-tight">
                {{ isLoading ? "..." : data?.totalCount }}
              </p>
            </div>
            <UIcon name="i-lucide-activity" class="w-8 h-8 opacity-50" />
          </div>
        </UCard>
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
          <div class="flex gap-4" v-for="i in 6" :key="i">
            <div class="flex flex-col items-center">
              <USkeleton class="h-8 w-8 rounded-full shrink-0" />
              <div class="w-px h-full bg-gray-200 dark:bg-gray-800 mt-2" />
            </div>
            <div class="flex-1 pb-6">
              <USkeleton class="h-4 w-24 mb-2" />
              <USkeleton class="h-5 w-64 mb-1" />
            </div>
          </div>
        </div>

        <!-- Pagination Skeleton -->
        <div
          class="flex justify-center items-center gap-2 mt-6 pt-6 border-t border-gray-200 dark:border-gray-800"
        >
          <USkeleton class="h-9 w-9 rounded-md" v-for="i in 5" :key="i" />
        </div>
      </UCard>

      <!-- Timeline Section -->
      <UCard v-else-if="data">
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-xl font-semibold">Recent activity</h2>
            <UBadge variant="subtle" color="info">{{ data.totalCount }}</UBadge>
          </div>
        </template>

        <div class="space-y-4">
          <UTimeline :items="items" />
          <div
            class="flex justify-center pt-4 mt-2 border-t border-gray-200 dark:border-gray-800"
          >
            <UPagination
              v-model:page="activityStore.page"
              :total="data.totalCount"
              @update:page="changePage"
            />
          </div>
        </div>
      </UCard>

      <!-- Empty State -->
      <UCard v-else>
        <div class="max-w-md mx-auto text-center py-16">
          <UIcon
            name="i-lucide-folder-open"
            class="w-12 h-12 mx-auto mb-4 opacity-50"
          />
          <h3 class="text-xl font-semibold mb-2">No activity history yet</h3>
          <p class="text-sm opacity-70">
            Your activity will appear here as you interact with the system
          </p>
        </div>
      </UCard>
    </div>
  </div>
</template>
