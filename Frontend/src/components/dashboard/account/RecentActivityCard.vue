<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useElementSize } from "@vueuse/core";
import { computed, useTemplateRef } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import { getAuditMessage } from "@/composables/useAuditMessage";
import { AuditSortBy } from "@/enums/audit-sort-by";
import { OperationType } from "@/enums/operation-type";
import { SortDirection } from "@/enums/SortDirection";
import { auditEntityName, auditEntityRoute, collectGoneEntityIds } from "@/features/audit/links";
import { auditIcon, auditIndicatorClass, entityLabel } from "@/features/audit/presentation";
import { personalPaginated } from "@/queries/activities";
import { useAuthStore } from "@/stores/auth";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";

const { fill = false } = defineProps<{
  // The page gives the card a fixed height, so the list shows as many rows as fit it.
  fill?: boolean;
}>();

// Sign-ins and sign-outs have their own card, so extra rows are fetched to make up for them.
const FETCH_SIZE = 40;
const DEFAULT_ROW_COUNT = 6;
const MIN_FILL_ROW_COUNT = 3;

// Two text lines, the row padding and the divider; replaced by the real height once a row renders.
const FALLBACK_ROW_HEIGHT = 59;

const SESSION_OPERATIONS = new Set([OperationType.Login, OperationType.Logout]);

const authStore = useAuthStore();
const viewerId = computed(() => authStore.user?.user.id);

const { data, isPending, error, refetch } = useQuery(() =>
  personalPaginated({
    query: {
      page: 1,
      pageSize: FETCH_SIZE,
      sortBy: AuditSortBy.Timestamp,
      sortDirection: SortDirection.Desc,
    },
    userId: viewerId.value ?? "",
  }),
);

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const rows = computed(() =>
  (data.value?.items ?? []).filter((row) => !SESSION_OPERATIONS.has(row.operationType)),
);

const gone = computed(() => collectGoneEntityIds(rows.value));

const viewport = useTemplateRef<HTMLElement>("viewport");
const rowElements = useTemplateRef<HTMLElement[]>("rowElements");

const { height: viewportHeight } = useElementSize(viewport);
const { height: measuredRowHeight } = useElementSize(() => rowElements.value?.[0], undefined, {
  box: "border-box",
});

const rowCount = computed(() => {
  if (!fill || viewportHeight.value === 0) return DEFAULT_ROW_COUNT;

  // The first row has no divider above it, so one pixel is added back for the rows after it.
  const rowHeight = measuredRowHeight.value ? measuredRowHeight.value + 1 : FALLBACK_ROW_HEIGHT;

  return Math.max(MIN_FILL_ROW_COUNT, Math.floor(viewportHeight.value / rowHeight));
});

const visibleRows = computed(() =>
  rows.value.slice(0, rowCount.value).map((row) => ({
    icon: auditIcon(row.eventCode, row.entityType),
    indicator: auditIndicatorClass(row.eventCode, row.operationType),
    message: getAuditMessage(row.eventCode),
    name: auditEntityName(row) ?? entityLabel(row.entityType),
    route: auditEntityRoute(row, viewerId.value, gone.value),
    row,
  })),
);
</script>

<template>
  <section
    class="flex flex-col overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    aria-labelledby="recent-activity-heading"
  >
    <div
      class="flex items-start justify-between gap-3 border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70"
    >
      <div class="min-w-0">
        <h2
          id="recent-activity-heading"
          class="text-sm font-semibold text-gray-900 dark:text-gray-100"
        >
          Recent activity
        </h2>
        <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
          The latest changes made to your files, folders and library.
        </p>
      </div>

      <UButton
        :to="{ name: 'access-history' }"
        color="neutral"
        variant="ghost"
        size="xs"
        trailing-icon="i-lucide-arrow-right"
        class="shrink-0"
      >
        View all
      </UButton>
    </div>

    <div v-if="visibleError" class="flex flex-1 px-4 py-8 md:px-5">
      <GlassEmptyState
        icon="i-lucide-triangle-alert"
        title="Couldn't load your recent activity"
        :description="visibleError.message"
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="refetch()">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <div v-else-if="isPending && !data" class="flex h-24 flex-1 items-center justify-center">
      <BlockSpinner :size="20" aria-label="Loading recent activity" class="text-gray-500" />
    </div>

    <div v-else-if="rows.length === 0" class="flex flex-1 px-4 py-8 md:px-5">
      <GlassEmptyState
        icon="i-lucide-history"
        title="Nothing here yet"
        description="Uploads, renames, moves and deletes show up here as you make them."
        class="m-auto"
      />
    </div>

    <!-- Filling, the list sits absolutely so the rows it renders never feed back into its size. -->
    <div
      v-else
      ref="viewport"
      class="px-4 py-1 md:px-5"
      :class="{ 'relative min-h-44 flex-1': fill }"
    >
      <ul
        class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
        :class="{ 'absolute inset-x-4 inset-y-1 overflow-y-auto md:inset-x-5': fill }"
      >
        <li
          v-for="item in visibleRows"
          ref="rowElements"
          :key="item.row.id"
          class="flex items-center gap-3 py-2.5"
        >
          <span
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full"
            :class="item.indicator"
          >
            <UIcon :name="item.icon" class="h-3.5 w-3.5" />
          </span>

          <div class="min-w-0 flex-1">
            <p class="truncate text-sm text-gray-900 dark:text-gray-100">{{ item.message }}</p>
            <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
              <RouterLink
                v-if="item.route"
                :to="item.route"
                class="font-medium text-gray-700 hover:underline dark:text-gray-300"
              >
                {{ item.name }}
              </RouterLink>
              <span v-else class="font-medium text-gray-700 dark:text-gray-300">
                {{ item.name }}
              </span>
            </p>
          </div>

          <time
            :datetime="item.row.timestamp"
            :title="formatDateTime(item.row.timestamp)"
            class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
          >
            {{ formatTimeAgo(item.row.timestamp) }}
          </time>
        </li>
      </ul>
    </div>
  </section>
</template>
