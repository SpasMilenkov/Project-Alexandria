<template>
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
          @mouseenter="prefetchDetails"
          @touchstart.passive="prefetchDetails"
          @pointerenter="handlePointerEnter"
          @pointerleave="handlePointerLeave"
          @focus="handleFocus"
          @blur="handleBlur"
          :aria-describedby="describedBy ?? undefined"
        >
          <div
            class="relative shrink-0 flex items-center justify-center overflow-hidden rounded-md"
            :style="{ width: `${thumbnailBox.width * 1.4}px`, height: `${thumbnailBox.height}px` }"
          >
            <img
              v-if="showThumbnail"
              :src="thumbnailUrl"
              :alt="props.data.fileName"
              :width="thumbnailBox.width"
              :height="thumbnailBox.height"
              loading="lazy"
              decoding="async"
              class="h-full w-full object-contain"
              :class="{ invisible: !thumbnailLoaded }"
              @load="onThumbnailLoad"
              @error="onThumbnailError"
            />
            <div
              v-if="!showThumbnail || !thumbnailLoaded"
              class="absolute inset-0 flex items-center justify-center"
            >
              <Icon
                :icon="getFileIcon(props.data.fileName)"
                :width="iconSize"
                :height="iconSize"
                class="shrink-0"
              />
            </div>
          </div>
          <span class="text-sm text-center line-clamp-2 w-full wrap-break-word">
            {{ props.data.fileName }}
          </span>
        </button>
    </div>
  </UContextMenu>

  <UContextMenu v-else :items="contextMenuItems">
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
          @mouseenter="prefetchDetails"
          @touchstart.passive="prefetchDetails"
          @pointerenter="handlePointerEnter"
          @pointerleave="handlePointerLeave"
          @focus="handleFocus"
          @blur="handleBlur"
          :aria-describedby="describedBy ?? undefined"
        >
          <Icon
            :icon="getFileIcon(props.data.fileName)"
            :width="iconSize"
            :height="iconSize"
            class="shrink-0"
          />
          <span class="flex-1 truncate">{{ props.data.fileName }}</span>
          <span class="text-xs opacity-70 shrink-0 min-w-15 text-right">
            {{ formatBytes(Number(props.data.currentVersion.size)) }}
          </span>
        </button>
    </div>
  </UContextMenu>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQueryCache } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed } from "vue";

import type { TagDto } from "@/api/tag";

import { type FileResult } from "@/api/file";
import { type FileTooltipEnterKind } from "@/composables/useFileTooltip";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { getFile, getVersionsForFile } from "@/queries/files";
import { getPreview } from "@/queries/files";
import { getTagsForFile } from "@/queries/tags";
import { useSettingsStore } from "@/stores/settings";
import { getFileIcon } from "@/utils/icon.utils";
import { formatBytes } from "@/utils/size.utils";

const settingsStore = useSettingsStore();
const queryCache = useQueryCache();

const props = defineProps<{
  data: FileResult;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
  tags: TagDto[] | undefined;
  describedBy?: string | null;
}>();

const emit = defineEmits<{
  click: [event: MouseEvent];
  "open-details": [file: FileResult];
  rename: [fileId: string, originalName: string];
  delete: [fileIds: string[]];
  move: [fileIds: string[]];
  copy: [fileIds: string[]];
  download: [fileIds: string[]];
  share: [fileIds: string[]];
  contextmenu: [fileId: PointerEvent];
  "tooltip-enter": [file: FileResult, anchor: HTMLElement, kind: FileTooltipEnterKind];
  "tooltip-leave": [kind: FileTooltipEnterKind];
}>();

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

// Version-scoped thumbnail URL: permanently cacheable, browser + nginx do the
// work, no fetch layer. MIME guard skips the <img> for text/archive/unknown
// types that never produce a thumbnail backend-side (no 404 round-trip).
const {
  thumbnailUrl,
  thumbnailLoaded,
  thumbnailErrored,
  canHaveThumbnail,
  onThumbnailLoad,
  onThumbnailError,
} = useFileThumbnail(() => ({
  fileId: props.data.fileId,
  mimeType: props.data.mimeType,
  versionId: props.data.currentVersion.id,
}));

// Landscape tile scaled off the icon size (4:3, Windows-style proportions).
const thumbnailBox = computed(() => {
  const width = Math.round(iconSize.value * 1.5);
  return { height: Math.round((width * 3) / 4), width };
});

const showThumbnail = computed(
  () => settingsStore.thumbnailsEnabled && canHaveThumbnail.value && !thumbnailErrored.value,
);

const prefetchDetails = useDebounceFn(() => {
  queryCache.refresh(queryCache.ensure(getFile(props.data.fileId)));
  queryCache.refresh(queryCache.ensure(getTagsForFile(props.data.fileId)));
  queryCache.refresh(queryCache.ensure(getPreview(props.data.fileId)));
  queryCache.refresh(
    queryCache.ensure(getVersionsForFile({ id: props.data.fileId, page: 1, pageSize: 10 })),
  );
}, 150);

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canShare = (): boolean => true;
const canDelete = (): boolean => true;

const singleSelectMenuItems = [
  [
    {
      icon: "i-mdi-information-outline",
      label: "View details",
      kbds: [{ value: "alt" }, { value: "enter" }],
      onSelect: () => emit("open-details", props.data),
    },
    {
      disabled: !canDownload(),
      icon: "i-mdi-download-outline",
      kbds: [{ value: "D" }],
      label: "Download",
      onSelect: () => emit("download", [props.data.fileId]),
    },
  ],
  [
    {
      disabled: !canRename(),
      icon: "i-mdi-pencil-outline",
      kbds: ["R"],
      label: "Rename",
      onSelect: () => emit("rename", props.data.fileId, props.data.fileName),
    },
    {
      disabled: !canMove(),
      icon: "i-mdi-folder-move-outline",
      label: "Move to…",
      kbds: ["⌘", "X"],
      onSelect: () => emit("move", [props.data.fileId]),
    },
    {
      disabled: !canCopy(),
      icon: "i-mdi-content-copy",
      kbds: ["⌘", "C"],
      label: "Copy to…",
      onSelect: () => emit("copy", [props.data.fileId]),
    },
  ],
  [
    {
      disabled: !canShare(),
      icon: "i-mdi-share-variant-outline",
      label: "Share",
      onSelect: () => emit("share", [props.data.fileId]),
    },
  ],
  [
    {
      color: "error" as const,
      disabled: !canDelete(),
      icon: "i-mdi-delete-outline",
      kbds: ["Del"],
      label: "Delete",
      onSelect: () => emit("delete", [props.data.fileId]),
    },
  ],
];

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const count = props.selectedCount ?? 1;

  if (!isMultiSelect) return singleSelectMenuItems;

  return [
    [{ label: `${count} items selected`, type: "label" as const }],
    [
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: "Download all",
        onSelect: () => emit("download", []),
      },
    ],
    [
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move-outline",
        label: "Move all to…",
        onSelect: () => emit("move", []),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: "Copy all to…",
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canShare(),
        icon: "i-mdi-share-variant-outline",
        label: "Share all",
        onSelect: () => emit("share", []),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        onSelect: () => emit("delete", []),
      },
    ],
  ];
});

const handleClick = (event: MouseEvent) => emit("click", event);
const handleDoubleClick = () => emit("open-details", props.data);

const handlePointerEnter = (event: PointerEvent) => {
  if (event.pointerType === "touch") return;
  if (event.currentTarget instanceof HTMLElement) {
    emit("tooltip-enter", props.data, event.currentTarget, "pointer");
  }
};

const handlePointerLeave = () => emit("tooltip-leave", "pointer");

const handleFocus = (event: FocusEvent) => {
  if (event.currentTarget instanceof HTMLElement) {
    emit("tooltip-enter", props.data, event.currentTarget, "focus");
  }
};

const handleBlur = () => emit("tooltip-leave", "focus");
</script>

<style scoped></style>
