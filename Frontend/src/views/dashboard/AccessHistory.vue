<script setup lang="ts">
import { computed } from "vue";
import { useActivityStore } from "@/stores/activity";
import { SortDirection } from "@/enums/SortDirection";
import { EntityType, OperationType } from "@/api/activity";
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
  // What am I doing here???
  userId: authStore.user?.user.id,
}));

// Icon styles lookup
const FALLBACK_ICON_STYLE =
  "border-gray-300/60 text-gray-400 dark:border-gray-600/60 dark:text-gray-500";

const OP_ICON_STYLES: Partial<Record<OperationType, string>> = {
  [OperationType.Create]: "border-emerald-400/50 text-emerald-600 dark:text-emerald-400",
  [OperationType.Delete]: "border-rose-400/50 text-rose-500 dark:text-rose-400",
  [OperationType.Update]: "border-amber-400/50 text-amber-500 dark:text-amber-400",
  [OperationType.Login]: "border-sky-400/50 text-sky-500 dark:text-sky-400",
  [OperationType.Logout]: "border-sky-400/50 text-sky-500 dark:text-sky-400",
};

const opIconStyle = (op: OperationType): string => OP_ICON_STYLES[op] ?? FALLBACK_ICON_STYLE;

// Badge color
type OpColor = "success" | "error" | "warning" | "info" | "neutral";

const OP_COLORS: Partial<Record<OperationType, OpColor>> = {
  [OperationType.Create]: "success",
  [OperationType.Delete]: "error",
  [OperationType.Update]: "warning",
  [OperationType.Login]: "info",
  [OperationType.Logout]: "neutral",
};

const opColor = (op: OperationType): OpColor => OP_COLORS[op] ?? "neutral";

// Icon
const ENTITY_ICONS: Partial<Record<EntityType, string>> = {
  [EntityType.Directory]: "i-lucide-folder",
  [EntityType.File]: "i-lucide-file",
  [EntityType.Tag]: "i-lucide-tag",
  [EntityType.User]: "i-lucide-user",
};

const OP_ICONS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]: "i-lucide-plus-circle",
  [OperationType.Update]: "i-lucide-edit",
  [OperationType.Delete]: "i-lucide-trash-2",
  [OperationType.Login]: "i-lucide-log-in",
  [OperationType.Logout]: "i-lucide-log-out",
};

const getIcon = (op: OperationType, entity: EntityType): string =>
  ENTITY_ICONS[entity] ?? OP_ICONS[op] ?? "i-lucide-eye";

const getDefaultTitle = (op: OperationType, entity: EntityType): string =>
  `${OperationType[op].toLowerCase()} ${EntityType[entity].toLowerCase()}`;

// Rows
const rows = computed(() =>
  !data.value
    ? []
    : data.value.items.map((log) => ({
        color: opColor(log.operationType),
        date: new Date(log.timestamp).toLocaleDateString(undefined, {
          day: "numeric",
          month: "short",
          year: "numeric",
        }),
        icon: getIcon(log.operationType, log.entityTYpe),
        iconStyle: opIconStyle(log.operationType),
        key: log.entityId + log.timestamp,
        opLabel: OperationType[log.operationType] ?? "Unknown",
        operationType: log.operationType,
        time: new Date(log.timestamp).toLocaleTimeString(undefined, {
          hour: "2-digit",
          minute: "2-digit",
        }),
        title: log.description || getDefaultTitle(log.operationType, log.entityTYpe),
      })),
);

const changePage = (pageNumber: number) => {
  activityStore.page = pageNumber;
  refresh();
};
</script>

<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Header -->
    <div
      class="flex items-center justify-between gap-3 px-6 py-4 border-b border-gray-200/70 dark:border-gray-700/70"
    >
      <div class="flex items-center gap-2.5 min-w-0">
        <UIcon name="i-lucide-activity" class="w-5 h-5 text-muted shrink-0" />
        <h1 class="text-xl font-semibold truncate">Activity</h1>
        <UBadge v-if="data" color="neutral" variant="subtle" size="sm">
          {{ data.totalCount }}
        </UBadge>
      </div>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto">
      <div class="px-6 py-5 space-y-4">
        <!-- Error -->
        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          title="Failed to load activity"
          :description="error.message"
        />

        <!-- Loading skeleton -->
        <div v-if="isLoading" class="space-y-1">
          <div v-for="i in 10" :key="i" class="flex items-center gap-4 px-4 py-4">
            <USkeleton class="w-9 h-9 rounded-full shrink-0" />
            <div class="flex-1 space-y-2">
              <USkeleton class="h-4 w-72" />
              <USkeleton class="h-3 w-36 opacity-50" />
            </div>
            <USkeleton class="h-6 w-16 rounded-full shrink-0" />
          </div>
        </div>

        <!-- Log list -->
        <div v-else-if="rows.length > 0" class="relative">
          <!-- Vertical spine -->
          <div
            class="absolute left-[1.55rem] top-0 bottom-0 w-px border-l border-dashed border-gray-300/40 dark:border-gray-600/40 pointer-events-none"
          />

          <!-- Rows -->
          <div class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
            <div
              v-for="row in rows"
              :key="row.key"
              class="relative flex items-center gap-4 py-3 hover:bg-neutral-300/60 dark:hover:bg-white/5 transition-colors rounded-lg px-1"
            >
              <!-- Icon dot -->
              <div
                class="relative z-10 flex items-center justify-center w-9 h-9 rounded-full border-2 shrink-0 transition-colors bg-neutral-50 dark:bg-gray-950"
                :class="row.iconStyle"
              >
                <UIcon :name="row.icon" class="w-4 h-4" />
              </div>

              <!-- Title -->
              <p class="flex-1 min-w-0 text-sm font-medium leading-snug truncate">
                {{ row.title }}
              </p>

              <!-- Timestamp — own right-aligned column, never inline -->
              <p class="shrink-0 text-xs text-muted tabular-nums whitespace-nowrap hidden sm:block">
                {{ row.date }} · {{ row.time }}
              </p>

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
        <div v-else class="flex flex-col items-center justify-center py-24 text-center gap-3">
          <UIcon name="i-lucide-folder-open" class="w-12 h-12 text-muted" />
          <div class="space-y-1">
            <p class="text-sm font-medium">No activity yet</p>
            <p class="text-xs text-muted">Actions will appear here as you use the system</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
