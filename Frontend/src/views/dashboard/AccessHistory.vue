<script setup lang="ts">
import { computed } from "vue";
import { useActivityStore } from "@/stores/activity";
import { SortDirection } from "@/enums/SortDirection";
import { OperationType, EntityType } from "@/api/activity";
import { useAuthStore } from "@/stores/auth";
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

// ─── Icon helpers ─────────────────────────────────────────────────────────────

const getIconForOperation = (opType: OperationType, entityType: EntityType) => {
  if (entityType === EntityType.Directory) return "i-lucide-folder";
  if (entityType === EntityType.File) return "i-lucide-file";
  if (entityType === EntityType.Tag) return "i-lucide-tag";
  if (entityType === EntityType.User) return "i-lucide-user";

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

// ─── Color coding by operation type ──────────────────────────────────────────

type OpColor = "success" | "error" | "warning" | "info" | "neutral";

const opColor = (opType: OperationType): OpColor => {
  switch (opType) {
    case OperationType.Create:
      return "success";
    case OperationType.Delete:
      return "error";
    case OperationType.Update:
      return "warning";
    case OperationType.Login:
      return "info";
    case OperationType.Logout:
      return "neutral";
    default:
      return "neutral";
  }
};

const opLabel = (opType: OperationType): string =>
  OperationType[opType] ?? "Unknown";

// ─── Formatted log rows ───────────────────────────────────────────────────────

const rows = computed(() =>
  !data.value
    ? []
    : data.value.items.map((log) => ({
        key: log.entityId + log.timestamp,
        icon: getIconForOperation(log.operationType, log.entityTYpe),
        title:
          log.description || getDefaultTitle(log.operationType, log.entityTYpe),
        date: new Date(log.timestamp).toLocaleDateString(undefined, {
          year: "numeric",
          month: "short",
          day: "numeric",
        }),
        time: new Date(log.timestamp).toLocaleTimeString(undefined, {
          hour: "2-digit",
          minute: "2-digit",
        }),
        operationType: log.operationType,
        entityType: log.entityTYpe,
        color: opColor(log.operationType),
        opLabel: opLabel(log.operationType),
      })),
);

// ─── Pagination ───────────────────────────────────────────────────────────────

const changePage = (pageNumber: number) => {
  activityStore.page = pageNumber;
  refresh();
};
</script>

<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Header -->
    <div
      class="flex w-full gap-3 px-6 py-4 border-b items-center justify-between"
    >
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-50">
          <UIcon name="i-lucide-activity" class="w-4 h-4" />
        </div>
        <div>
          <h1 class="text-lg font-semibold tracking-tight">Activity</h1>
          <p class="text-xs opacity-90">
            Your recent actions across the system
          </p>
        </div>
      </div>
      <!-- Total badge -->
      <div class="text-right" v-if="data">
        <p class="text-xs uppercase tracking-widest font-medium">
          Total
        </p>
        <p class="text-sm font-semibold tabular-nums">{{ data.totalCount }}</p>
      </div>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto">
      <div class="w-full max-w-5xl mx-auto px-4 sm:px-8 py-6">
        <!-- Error -->
        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          title="Failed to load activity"
          :description="error.message"
          class="mb-6"
        />

        <!-- Loading skeleton -->
        <div v-if="isLoading" class="space-y-1">
          <div
            v-for="i in 10"
            :key="i"
            class="flex items-center gap-4 px-4 py-4"
          >
            <USkeleton class="w-9 h-9 rounded-full shrink-0" />
            <div class="flex-1 space-y-2">
              <USkeleton class="h-4 w-72" />
              <USkeleton class="h-3 w-36 opacity-50" />
            </div>
            <USkeleton class="h-6 w-16 rounded-full shrink-0" />
          </div>
        </div>

        <!-- Log list -->
        <div v-else-if="rows.length > 0">
          <div class="relative">
            <!-- Vertical spine -->
            <div
              class="absolute left-[1.55rem] top-0 bottom-0 w-px border-l border-dashed opacity-10 pointer-events-none"
            />

            <div
              v-for="row in rows"
              :key="row.key"
              class="relative flex items-center gap-5 group py-1"
            >
              <!-- Icon dot -->
              <div
                class="relative z-10 flex items-center justify-center w-9 h-9 rounded-full border-2 shrink-0 transition-colors"
                :class="[
                  row.operationType === OperationType.Delete
                    ? 'border-rose-400/50 text-rose-500 dark:text-rose-400'
                    : row.operationType === OperationType.Create
                      ? 'border-emerald-400/50 text-emerald-600 dark:text-emerald-400'
                      : row.operationType === OperationType.Update
                        ? 'border-amber-400/50 text-amber-500 dark:text-amber-400'
                        : row.operationType === OperationType.Login ||
                            row.operationType === OperationType.Logout
                          ? 'border-sky-400/50 text-sky-500 dark:text-sky-400'
                          : 'border-dashed border-current opacity-80',
                ]"
              >
                <UIcon :name="row.icon" class="w-4 h-4" />
              </div>

              <!-- Row content -->
              <div
                class="flex-1 flex items-center justify-between gap-6 py-3.5 border-b border-dashed opacity-90 group-last:border-0"
              >
                <!-- Title + date -->
                <div class="min-w-0 flex items-baseline gap-4">
                  <p class="text-sm font-medium leading-snug truncate">
                    {{ row.title }}
                  </p>
                  <p
                    class="text-xs tabular-nums whitespace-nowrap shrink-0"
                  >
                    {{ row.date }} · {{ row.time }}
                  </p>
                </div>

                <!-- Badge -->
                <UBadge
                  :color="row.color"
                  variant="subtle"
                  size="sm"
                  class="shrink-0 capitalize font-medium"
                >
                  {{ row.opLabel }}
                </UBadge>
              </div>
            </div>
          </div>

          <!-- Pagination -->
          <div class="flex justify-center pt-8 mt-2">
            <UPagination
              v-model:page="activityStore.page"
              :total="data!.totalCount"
              :page-size="activityStore.pageSize"
              @update:page="changePage"
            />
          </div>
        </div>

        <!-- Empty state -->
        <div v-else class="text-center py-24 space-y-3 opacity-35">
          <UIcon name="i-lucide-folder-open" class="w-12 h-12 mx-auto" />
          <div>
            <p class="font-medium">No activity yet</p>
            <p class="text-sm mt-1">
              Actions will appear here as you use the system
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
