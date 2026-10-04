<template>
  <div v-if="viewMode === 'grid'" class="relative group" tabindex="0" :data-file-id="data.fileId">
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
      @touchend="cancelPrefetch"
      @touchcancel="cancelPrefetch"
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
        <span
          v-if="fileExtension"
          class="absolute bottom-1 right-1 text-xs leading-4 px-1.5 rounded bg-neutral-900/80 text-white shrink-0 max-w-16 truncate"
        >
          {{ fileExtension }}
        </span>
      </div>
      <span class="text-sm text-center line-clamp-2 w-full wrap-break-word">
        {{ props.data.fileName }}
      </span>
    </button>
  </div>

  <div v-else class="relative group px-2" tabindex="0" :data-file-id="data.fileId">
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
      @mouseenter="prefetchDetails"
      @touchstart.passive="prefetchDetails"
      @touchend="cancelPrefetch"
      @touchcancel="cancelPrefetch"
      @pointerenter="handlePointerEnter"
      @pointerleave="handlePointerLeave"
      @focus="handleFocus"
      @blur="handleBlur"
      :aria-describedby="describedBy ?? undefined"
    >
      <span class="flex items-center justify-center w-8 h-8 rounded-lg bg-gray-500/10 shrink-0">
        <Icon
          :icon="getFileIcon(props.data.fileName)"
          :width="iconSize"
          :height="iconSize"
          class="shrink-0 text-gray-600 dark:text-gray-400"
        />
      </span>
      <span class="flex-1 min-w-0 flex items-center gap-2">
        <span class="truncate">{{ props.data.fileName }}</span>
        <span
          v-if="fileExtension"
          class="text-xs leading-4 px-1.5 rounded border border-gray-200/70 dark:border-gray-700/70 text-gray-600 dark:text-gray-400 shrink-0 ml-auto"
        >
          {{ fileExtension }}
        </span>
      </span>
      <span class="hidden sm:block text-xs text-gray-500 dark:text-gray-500 tabular-nums shrink-0">
        {{ modifiedLabel }}
      </span>
      <span
        class="text-xs text-gray-500 dark:text-gray-500 tabular-nums shrink-0 min-w-15 text-right"
      >
        {{ formatBytes(Number(props.data.currentVersion.size)) }}
      </span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQueryCache } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, onUnmounted } from "vue";

import { type FileResult } from "@/api/file";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { type FileTooltipEnterKind } from "@/composables/useFileTooltip";
import { getFile, getPreview, getVersionsForFile } from "@/queries/files";
import { getTagsForFile } from "@/queries/tags";
import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";
import { getFileExtension } from "@/utils/file-name.utils";
import { getFileIcon } from "@/utils/icon.utils";
import { formatBytes } from "@/utils/size.utils";

const settingsStore = useSettingsStore();
const queryCache = useQueryCache();

const props = defineProps<{
  data: FileResult;
  viewMode: "grid" | "list";
  isSelected: boolean;
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

const fileExtension = computed(() => getFileExtension(props.data.fileName));

const modifiedLabel = computed(() => formatDate(props.data.updatedAt || props.data.createdAt));

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

// The four requests above only warm the details drawer, which fetches them for
// real on open. A pending warmup is cancelled as soon as interest moves away
// (pointer/focus leave, touch end) or the row unmounts, so no delayed request
// begins after disposal.
const cancelPrefetch = () => {
  prefetchDetails.cancel();
};

onUnmounted(() => {
  cancelPrefetch();
});

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

const handlePointerLeave = () => {
  cancelPrefetch();
  emit("tooltip-leave", "pointer");
};

const handleFocus = (event: FocusEvent) => {
  if (event.currentTarget instanceof HTMLElement) {
    emit("tooltip-enter", props.data, event.currentTarget, "focus");
  }
};

const handleBlur = () => {
  cancelPrefetch();
  emit("tooltip-leave", "focus");
};
</script>

<style scoped></style>
