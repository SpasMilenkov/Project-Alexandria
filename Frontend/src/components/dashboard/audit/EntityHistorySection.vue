<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { EntityType } from "@/enums/entity-type";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import { toAuditRouteQuery } from "@/features/audit/query";
import { entityHistory } from "@/queries/activities";
import { useAuthStore } from "@/stores/auth";

import AuditTimeline from "./AuditTimeline.vue";

const { entityType, entityId, enabled } = defineProps<{
  entityType: EntityType;
  entityId: string;
  // Drawers keep this mounted while closed; the history only loads while it is visible.
  enabled: boolean;
}>();

const STEP = 10;
const MAX_ROWS = 100;

const authStore = useAuthStore();
const viewerId = computed(() => authStore.user?.user.id);

const pageSize = ref(STEP);

watch(
  () => entityId,
  () => {
    pageSize.value = STEP;
  },
);

const { data, isPending, error } = useQuery(() => ({
  ...entityHistory({ entityId, entityType, page: 1, pageSize: pageSize.value }),
  enabled: enabled && Boolean(entityId),
}));

const rows = computed(() => data.value?.items ?? []);

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const hasMore = computed(() => Boolean(data.value) && data.value!.totalCount > rows.value.length);

const canLoadMore = computed(() => hasMore.value && pageSize.value < MAX_ROWS);

const loadMore = () => {
  pageSize.value = Math.min(MAX_ROWS, pageSize.value + STEP);
};

const fullHistoryLink = computed(() => ({
  name: "access-history",
  query: toAuditRouteQuery({ entityId }),
}));
</script>

<template>
  <DetailsSection title="History">
    <template v-if="data && data.totalCount > 0" #actions>
      <UButton
        :to="fullHistoryLink"
        label="Open in activity"
        icon="i-lucide-arrow-up-right"
        size="xs"
        color="neutral"
        variant="ghost"
      />
    </template>

    <UAlert
      v-if="visibleError"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Couldn't load the history"
      :description="visibleError.message"
    />

    <div v-else-if="isPending && !data" class="flex h-16 items-center justify-center text-gray-500">
      <BlockSpinner :size="20" aria-label="Loading history" />
    </div>

    <template v-else-if="rows.length > 0">
      <AuditTimeline :rows="rows" :viewer-id="viewerId" compact />

      <UButton
        v-if="canLoadMore"
        label="Show older changes"
        size="xs"
        color="neutral"
        variant="outline"
        class="self-start"
        @click="loadMore"
      />
    </template>

    <div v-else class="flex flex-col items-center gap-2 py-6 text-center">
      <UIcon name="i-lucide-history" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No history yet</p>
      <p class="text-xs text-gray-600 dark:text-gray-400">
        Changes to this item will be listed here.
      </p>
    </div>
  </DetailsSection>
</template>
