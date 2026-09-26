<template>
  <!-- Grid View -->
  <div v-if="viewMode === 'grid'" class="relative group" tabindex="0" :data-dir-id="data.id">
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
  <div v-else class="relative group" tabindex="0" :data-dir-id="data.id">
    <button
      type="button"
      class="w-full flex items-center gap-3 px-4 py-2 transition-colors cursor-pointer text-left border-b last:border-b-0"
      :class="[
        isSelected
          ? 'bg-primary/20 ring-2 ring-primary'
          : 'hover:bg-primary/40 dark:hover:bg-primary/35',
      ]"
      @click="handleClick"
      @dblclick="handleDoubleClick"
    >
      <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
      <span class="flex-1 truncate font-medium">{{ data.name }}</span>
      <Icon icon="mdi:chevron-right" class="w-4 h-4 shrink-0" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();

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

// Row actions live in the shared explorer-owned context menu. Rows forward
// click intent and keep no menu trees, menu-building state, or global
// shortcuts of their own.

const handleClick = (event: MouseEvent) => emit("click", event);

const handleDoubleClick = () => {
  emit("navigate", props.data.id, props.data.name);
};
</script>

<style scoped></style>
