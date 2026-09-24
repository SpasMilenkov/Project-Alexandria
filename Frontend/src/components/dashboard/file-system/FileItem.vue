<template>
  <div
    v-if="viewMode === 'grid'"
    class="relative group"
    tabindex="0"
    :data-file-id="data.fileId"
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

  <div
    v-else
    class="relative group"
    tabindex="0"
    :data-file-id="data.fileId"
  >
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
  tags: TagDto[] | undefined;
  describedBy?: string | null;
}>();

const emit = defineEmits<{
  click: [event: MouseEvent];
  "open-details": [file: FileResult];
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

const handleClick = (event: MouseEvent) => emit("click", event);
const handleDoubleClick = () => emit("open-details", props.data);

// Row actions live in the shared explorer-owned context menu. Rows forward
// click and tooltip intent and keep no menu trees or menu-building state.

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
