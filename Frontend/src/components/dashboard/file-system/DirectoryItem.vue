<template>
  <!-- Grid View -->
  <UContextMenu
    v-if="viewMode === 'grid'"
    :items="contextMenuItems"
    :ui="{ content: 'lg:min-w-56' }"
  >
    <div class="relative group" tabindex="0">
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
        @contextmenu="emit('contextmenu', $event)"
      >
        <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
        <span class="text-sm text-center line-clamp-2 w-full wrap-break-word font-medium">
          {{ data.name }}
        </span>
      </button>
    </div>
  </UContextMenu>

  <!-- List View -->
  <UContextMenu v-else :items="contextMenuItems" :ui="{ content: 'lg:min-w-56' }">
    <div class="relative group" tabindex="0">
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
        @contextmenu="emit('contextmenu', $event)"
      >
        <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
        <span class="flex-1 truncate font-medium">{{ data.name }}</span>
        <Icon icon="mdi:chevron-right" class="w-4 h-4 shrink-0" />
      </button>
    </div>
  </UContextMenu>
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
  selectedCount?: number;
}>();

const emit = defineEmits<{
  navigate: [directoryId: string, dirName: string];
  click: [event: MouseEvent];
  open: [directoryId: string];
  rename: [directoryId: string];
  delete: [directoryIds: string[]];
  move: [directoryIds: string];
  copy: [directoryIds: string[]];
  download: [directoryIds: string[]];
  contextmenu: [event: PointerEvent];
  "open-details": [directory: DirectorySummaryDto];
}>();

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canDelete = (): boolean => true;

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const count = props.selectedCount ?? 1;

  if (!isMultiSelect) {
    return [
      [
        {
          icon: "i-mdi-information-outline",
          label: "View details",
          kbds: [{ value: "alt" }, { value: "enter" }],
          onSelect: () => {
            emit("open-details", props.data);
          },
        },
        {
          icon: "i-mdi-folder-open",
          label: "Open",
          onSelect: () => emit("open", props.data.id),
        },
      ],
      [
        {
          disabled: !canRename(),
          icon: "i-mdi-pencil-outline",
          label: "Rename",
          kbds: ["R"],
          onSelect: () => emit("rename", props.data.id),
        },
      ],
      [
        {
          disabled: !canMove(),
          icon: "i-mdi-folder-move-outline",
          label: "Move to…",
          kbds: ["⌘", "X"],
          onSelect: () => emit("move", props.data.id),
        },
        {
          disabled: !canCopy(),
          icon: "i-mdi-content-copy",
          label: "Copy to…",
          kbds: ["⌘", "C"],
          onSelect: () => emit("copy", [props.data.id]),
        },
        {
          disabled: !canDownload(),
          icon: "i-mdi-download-outline",
          label: "Download",
          kbds: ["D"],
          onSelect: () => emit("download", [props.data.id]),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-mdi-delete-outline",
          label: "Delete",
          kbds: ["Del"],
          onSelect: () => emit("delete", [props.data.id]),
        },
      ],
    ];
  }

  return [
    [
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move-outline",
        label: `Move ${count} items to…`,
        kbds: ["⌘", "X"],
        onSelect: () => emit("move", props.data.id),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: `Copy ${count} items to…`,
        kbds: ["⌘", "C"],
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: `Download ${count} items`,
        kbds: ["D"],
        onSelect: () => emit("download", []),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        kbds: ["Del"],
        onSelect: () => emit("delete", []),
      },
    ],
  ];
});

// Folder actions stay available through context menus and the shared
// explorer-owned details drawer. Keyboard shortcuts are owned once by the
// explorer and apply to the current selection, so rows must not register
// global shortcuts of their own. Details intent is emitted to the shared
// owner; rows keep no drawer controllers or component-handle registries.

// Permission check stubs

const handleClick = (event: MouseEvent) => emit("click", event);

const handleDoubleClick = () => {
  emit("navigate", props.data.id, props.data.name);
};
</script>

<style scoped></style>
