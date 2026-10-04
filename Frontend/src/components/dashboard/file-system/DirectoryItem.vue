<template>
  <!-- Grid View -->
  <div
    v-if="viewMode === 'grid'"
    class="relative group"
    tabindex="0"
    :data-dir-id="data.id"
    @mouseenter="prefetchPolicy"
    @mouseleave="cancelPrefetch"
    @focusin="prefetchPolicy"
    @focusout="cancelPrefetch"
    @touchstart.passive="prefetchPolicy"
    @touchend="cancelPrefetch"
    @touchcancel="cancelPrefetch"
  >
    <button
      type="button"
      class="w-full flex flex-col items-center gap-2 p-4 rounded-lg transition-colors cursor-pointer"
      :class="[
        isSelected
          ? 'bg-primary/20 ring-2 ring-primary'
          : 'hover:bg-primary/40 dark:hover:bg-primary/35',
      ]"
      @click="handleClick"
      @dblclick="handleDoubleClick"
    >
      <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
      <span class="text-sm text-center line-clamp-2 w-full wrap-break-word font-medium">
        {{ data.name }}
      </span>
    </button>
  </div>

  <!-- List View -->
  <div
    v-else
    class="relative group px-2"
    tabindex="0"
    :data-dir-id="data.id"
    @mouseenter="prefetchPolicy"
    @mouseleave="cancelPrefetch"
    @focusin="prefetchPolicy"
    @focusout="cancelPrefetch"
    @touchstart.passive="prefetchPolicy"
    @touchend="cancelPrefetch"
    @touchcancel="cancelPrefetch"
  >
    <button
      type="button"
      class="w-full flex items-center gap-3 px-2 py-2 rounded-lg transition-colors cursor-pointer text-left"
      :class="[
        isSelected
          ? 'bg-primary/20 ring-2 ring-primary'
          : 'hover:bg-primary/40 dark:hover:bg-primary/35',
      ]"
      @click="handleClick"
      @dblclick="handleDoubleClick"
    >
      <span class="flex items-center justify-center w-8 h-8 rounded-lg bg-gray-500/10 shrink-0">
        <Icon
          icon="mdi:folder"
          :width="iconSize"
          :height="iconSize"
          class="shrink-0 text-gray-600 dark:text-gray-400"
        />
      </span>
      <span class="flex-1 truncate font-medium">{{ data.name }}</span>
      <span class="hidden sm:block text-xs text-gray-500 dark:text-gray-500 tabular-nums shrink-0">
        {{ modifiedLabel }}
      </span>
      <Icon icon="mdi:chevron-right" class="w-4 h-4 shrink-0 text-gray-500 dark:text-gray-500" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQueryCache } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, onUnmounted } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import { getPolicyByDirectory } from "@/queries/policies";
import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";

const settingsStore = useSettingsStore();
const queryCache = useQueryCache();

const props = defineProps<{
  data: DirectorySummaryDto;
  viewMode: "grid" | "list";
  isSelected: boolean;
}>();

const emit = defineEmits<{
  navigate: [directoryId: string, dirName: string];
  click: [event: MouseEvent];
}>();

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

const modifiedLabel = computed(() => formatDate(props.data.updatedAt || props.data.createdAt));

const prefetchPolicy = useDebounceFn(() => {
  // Failed warmups stay silent; opening the drawer can retry the query.
  void queryCache
    .refresh(queryCache.ensure(getPolicyByDirectory(props.data.id)))
    .catch(() => undefined);
}, 150);

const cancelPrefetch = () => {
  prefetchPolicy.cancel();
};

onUnmounted(cancelPrefetch);

// Row actions live in the shared explorer-owned context menu. Rows forward
// click intent and keep no menu trees, menu-building state, or global
// shortcuts of their own.

const handleClick = (event: MouseEvent) => emit("click", event);

const handleDoubleClick = () => {
  emit("navigate", props.data.id, props.data.name);
};
</script>

<style scoped></style>
