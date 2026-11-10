<template>
  <!-- Grid View -->
  <div
    v-if="viewMode === 'grid'"
    class="relative group"
    tabindex="0"
    :data-dir-id="data.id"
    :data-drop-dir-id="data.id"
    :data-drop-dir-name="data.name"
    data-drop-kind="item"
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
      class="flex h-full w-full cursor-pointer flex-col gap-2 rounded-xl border p-2 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary group-data-[drop-active=valid]:border-primary group-data-[drop-active=valid]:bg-primary/10 group-data-[drop-active=valid]:ring-2 group-data-[drop-active=valid]:ring-primary"
      :class="[
        isSelected
          ? 'border-primary bg-primary/10 ring-1 ring-primary'
          : 'border-gray-200/70 bg-gray-500/5 hover:border-gray-300/80 hover:bg-gray-500/10 dark:border-gray-700/70 dark:bg-gray-400/5 dark:hover:border-gray-600/80 dark:hover:bg-white/10',
      ]"
      @click="handleClick"
      @dblclick="handleDoubleClick"
    >
      <div
        class="relative flex aspect-[4/3] w-full items-center justify-center rounded-lg bg-gray-500/[0.07] text-gray-500 dark:bg-white/[0.06] dark:text-gray-400"
      >
        <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
        <Icon
          v-if="data.isStarred"
          icon="mdi:star"
          class="absolute top-2 left-2 w-4 h-4 text-gray-600 dark:text-gray-400"
          role="img"
          aria-hidden="false"
          aria-label="Favorite folder"
        />
        <span
          v-if="isSelected"
          class="absolute top-2 right-2 grid h-5 w-5 place-items-center rounded-full bg-primary text-white dark:text-neutral-900"
          aria-hidden="true"
        >
          <Icon icon="mdi:check" class="w-3 h-3" />
        </span>
      </div>
      <span
        class="line-clamp-2 min-h-10 w-full wrap-break-word px-0.5 text-sm font-medium leading-5 text-gray-900 dark:text-gray-100"
      >
        {{ data.name }}
      </span>
      <span class="truncate px-0.5 pb-0.5 text-xs tabular-nums text-gray-600 dark:text-gray-400">
        {{ modifiedLabel }}
      </span>
    </button>
  </div>

  <!-- List View -->
  <div
    v-else
    class="relative group"
    tabindex="0"
    :data-dir-id="data.id"
    :data-drop-dir-id="data.id"
    :data-drop-dir-name="data.name"
    data-drop-kind="item"
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
      class="grid h-11 w-full cursor-pointer grid-cols-[minmax(0,1fr)_16px] items-center gap-4 rounded-lg border-b border-gray-200/70 px-4 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary @min-[720px]/explorer:grid-cols-[minmax(0,1fr)_112px_16px] dark:border-gray-700/70 group-data-[drop-active=valid]:bg-primary/10 group-data-[drop-active=valid]:ring-2 group-data-[drop-active=valid]:ring-inset group-data-[drop-active=valid]:ring-primary"
      :class="[
        isSelected
          ? 'bg-primary/10 ring-1 ring-inset ring-primary'
          : 'hover:bg-gray-500/5 dark:hover:bg-white/5',
      ]"
      @click="handleClick"
      @dblclick="handleDoubleClick"
    >
      <span class="flex min-w-0 items-center gap-3 text-gray-900 dark:text-gray-100">
        <Icon
          icon="mdi:folder"
          :width="iconSize"
          :height="iconSize"
          class="shrink-0 text-gray-500 dark:text-gray-400"
        />
        <span class="truncate font-medium">{{ data.name }}</span>
        <Icon
          v-if="data.isStarred"
          icon="mdi:star"
          class="w-4 h-4 shrink-0 text-gray-600 dark:text-gray-400"
          role="img"
          aria-hidden="false"
          aria-label="Favorite folder"
        />
      </span>
      <span
        class="hidden text-xs tabular-nums text-gray-600 @min-[720px]/explorer:block dark:text-gray-400"
      >
        {{ modifiedLabel }}
      </span>
      <Icon
        icon="mdi:chevron-right"
        class="w-4 h-4 justify-self-end text-gray-400 dark:text-gray-500"
      />
    </button>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQueryCache } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, onUnmounted } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import { useExplorerDragSession } from "@/composables/useExplorerDragSession";
import { getPolicyByDirectory } from "@/queries/policies";
import { useSettingsStore } from "@/stores/settings";
import { formatTimeAgo } from "@/utils/date-formatters";

const settingsStore = useSettingsStore();
const queryCache = useQueryCache();
const { isDragging } = useExplorerDragSession();

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

const modifiedLabel = computed(() => formatTimeAgo(props.data.updatedAt || props.data.createdAt));

const prefetchPolicy = useDebounceFn(() => {
  if (isDragging.value) return;

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
