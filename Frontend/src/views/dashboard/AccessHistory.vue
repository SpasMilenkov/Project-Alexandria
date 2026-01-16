<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { useActivityStore } from "@/stores/activity";
import { SortDirection } from "@/enums/SortDirection";
import { OperationType, EntityType } from "@/api/activity";
import { useAuthStore } from "@/stores/auth";
import type { TimelineItem } from "@nuxt/ui";
const activityStore = useActivityStore();
const authStore = useAuthStore();
const totalItems = ref(0);

const items = computed((): TimelineItem[] =>
  activityStore.getActivity.map((log) => ({
    date: new Date(log.timestamp).toLocaleDateString(),
    value: log.entityId + log.timestamp,
    icon: getIconForOperation(log.operationType, log.entityTYpe),
    title:
      log.description || getDefaultTitle(log.operationType, log.entityTYpe),
    operationType: log.operationType,
    entityType: log.entityTYpe,
  }))
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

const hasHistory = computed(() => activityStore.getActivity.length > 0);
const isLoading = computed(() => activityStore.isLoading);
const error = computed(() => activityStore.error);

const loadActivity = async () => {
  console.log("userId: ", authStore.user?.id);
  console.log(authStore.user);
  const result = await activityStore.fetchActivity({
    page: activityStore.page,
    pageSize: activityStore.pageSize,
    sortBy: "timestamp",
    sortDirection: SortDirection.Desc,
    userId: authStore.user?.id,
  });
  console.log("requestResult", result);
  if (result.success && result.data) {
    totalItems.value = result.data.totalCount;
  }
};

const changePage = (pageNumber: number) => {
  activityStore.page = pageNumber;
  loadActivity();
};

onMounted(async () => {
  await loadActivity();
});
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
        :description="error"
        class="mb-6"
      />

      <!-- Stats Card -->
      <UCard class="mb-6">
        <div class="flex items-center justify-between">
          <div>
            <p class="text-sm opacity-70 mb-1">Total Activities</p>
            <p class="text-2xl font-semibold">
              {{ isLoading ? "..." : totalItems }}
            </p>
          </div>
          <UIcon name="i-lucide-activity" class="w-8 h-8 opacity-50" />
        </div>
      </UCard>

      <!-- Loading State -->
      <UCard v-if="isLoading && !hasHistory">
        <div class="text-center py-12">
          <UIcon
            name="i-lucide-loader-2"
            class="w-12 h-12 mx-auto mb-4 opacity-50 animate-spin"
          />
          <p class="text-sm opacity-70">Loading activity history...</p>
        </div>
      </UCard>

      <!-- Timeline Section -->
      <UCard v-else-if="hasHistory">
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-xl font-semibold">Recent Activity</h2>
            <UBadge variant="subtle" color="info">{{
              activityStore.getTotalCount
            }}</UBadge>
          </div>
        </template>

        <UTimeline :items="items"> </UTimeline>
        <UPagination
          v-model:page="activityStore.page"
          :total="activityStore.totalCount"
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
