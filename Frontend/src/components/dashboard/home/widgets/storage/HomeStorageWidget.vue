<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { homeStorage } from "@/queries/home-widgets";
import { useHomeStore } from "@/stores/home";
import { formatBytes } from "@/utils/size.utils";

import HomeStorageDisplay from "./HomeStorageDisplay.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();

const query = useHomeWidgetQuery(
  () => homeStorage({ userId: store.ownerId ?? "", epoch: store.ownerEpoch }),
  (preview) => ({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, value: preview.storage }),
);

const data = computed(() => {
  const response = query.data.value;
  if (response?.userId !== store.ownerId || response.epoch !== store.ownerEpoch) return null;

  return response.value;
});

const authPending = computed(() => isAuthError(query.error.value));
</script>

<template>
  <div class="storage-widget flex min-h-0 flex-1 flex-col gap-2">
    <HomeWidgetDataState
      :loading="authPending || (!data && !query.error.value)"
      :failed="!data && Boolean(query.error.value)"
      @retry="query.refresh()"
    >
      <HomeStorageDisplay v-if="data" :data="data" :storage-style="widget.options.storageStyle" />

      <div
        v-if="data"
        class="storage-footer items-center justify-between gap-2 text-xs text-gray-600 dark:text-gray-400"
      >
        <RouterLink
          to="/my-storage"
          class="underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary"
          >View storage</RouterLink
        >
        <span v-if="data.trashSize > 0">Trash: {{ formatBytes(data.trashSize) }}</span>
      </div>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
.storage-footer {
  display: none;
}

@container home-widget (min-height: 240px) {
  .storage-footer {
    display: flex;
  }
}

@container home-widget (min-width: 420px) and (min-height: 300px) {
  .storage-footer {
    font-size: clamp(12px, min(1.5cqi, 3cqb), 18px);
  }
}
</style>
