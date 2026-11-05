<template>
  <div v-if="viewMode === 'grid'" class="relative group" tabindex="0" :data-file-id="data.fileId">
    <button
      type="button"
      class="flex h-full w-full cursor-pointer flex-col gap-2 rounded-xl border p-2 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
      :class="[
        isSelected
          ? 'border-primary bg-primary/10 ring-1 ring-primary'
          : 'border-gray-200/70 bg-gray-500/5 hover:border-gray-300/80 hover:bg-gray-500/10 dark:border-gray-700/70 dark:bg-gray-400/5 dark:hover:border-gray-600/80 dark:hover:bg-white/10',
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
      :aria-pressed="isSelected"
    >
      <div
        class="relative flex aspect-[4/3] w-full items-center justify-center overflow-hidden rounded-lg bg-gray-500/[0.07] text-gray-500 dark:bg-white/[0.06] dark:text-gray-400"
      >
        <img
          v-if="showThumbnail"
          :src="thumbnailUrl"
          :alt="props.data.fileName"
          :width="thumbnailBox.width"
          :height="thumbnailBox.height"
          loading="lazy"
          decoding="async"
          draggable="false"
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
          v-if="props.data.currentVersion.isEncrypted"
          class="absolute top-2 left-2 flex items-center justify-center rounded-md bg-neutral-900/80 p-0.5 text-white"
          data-encrypted-indicator
        >
          <Icon icon="mdi:lock" class="w-3.5 h-3.5" aria-hidden="true" />
          <span class="sr-only">Encrypted</span>
        </span>
        <span
          v-if="fileExtension"
          class="absolute bottom-2 left-2 max-w-16 truncate rounded border border-gray-200/70 px-1.5 text-[11px] leading-[18px] text-gray-700 frosted-glass glass-surface-strong dark:border-gray-700/70 dark:text-gray-200"
        >
          {{ fileExtension }}
        </span>
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
        {{ props.data.fileName }}
      </span>
      <span class="truncate px-0.5 pb-0.5 text-xs tabular-nums text-gray-600 dark:text-gray-400">
        {{ formatBytes(Number(props.data.currentVersion.size)) }} · {{ modifiedLabel }}
      </span>
    </button>
  </div>

  <div v-else class="relative group" tabindex="0" :data-file-id="data.fileId">
    <button
      type="button"
      class="grid h-11 w-full cursor-pointer grid-cols-[minmax(0,1fr)_56px_88px] items-center gap-4 rounded-lg border-b border-gray-200/70 px-4 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary @min-[720px]/explorer:grid-cols-[minmax(0,1fr)_56px_112px_88px] dark:border-gray-700/70"
      :class="[
        isSelected
          ? 'bg-primary/10 ring-1 ring-inset ring-primary'
          : 'hover:bg-gray-500/5 dark:hover:bg-white/5',
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
      :aria-pressed="isSelected"
    >
      <span class="flex min-w-0 items-center gap-3 text-gray-900 dark:text-gray-100">
        <Icon
          :icon="getFileIcon(props.data.fileName)"
          :width="iconSize"
          :height="iconSize"
          class="shrink-0 text-gray-500 dark:text-gray-400"
        />
        <span class="truncate">{{ props.data.fileName }}</span>
        <span
          v-if="props.data.currentVersion.isEncrypted"
          class="flex shrink-0 items-center text-gray-500 dark:text-gray-500"
          data-encrypted-indicator
        >
          <Icon icon="mdi:lock" class="w-3.5 h-3.5" aria-hidden="true" />
          <span class="sr-only">Encrypted</span>
        </span>
      </span>
      <span
        v-if="fileExtension"
        class="justify-self-start rounded border border-gray-200/70 px-1.5 text-[11px] leading-[18px] text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
      >
        {{ fileExtension }}
      </span>
      <span v-else />
      <span
        class="hidden text-xs tabular-nums text-gray-600 @min-[720px]/explorer:block dark:text-gray-400"
      >
        {{ modifiedLabel }}
      </span>
      <span class="text-right text-xs tabular-nums text-gray-600 dark:text-gray-400">
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

import { type FileResult, type PreviewResultDto } from "@/api/file";
import { useExplorerDragSession } from "@/composables/useExplorerDragSession";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { type FileTooltipEnterKind } from "@/composables/useFileTooltip";
import {
  FILES_QUERY_KEYS,
  getFile,
  getPreviewByVersion,
  getVersionsForFile,
} from "@/queries/files";
import { getTagsForFile } from "@/queries/tags";
import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";
import { getFileExtension } from "@/utils/files/file-name";
import { getFileIcon } from "@/utils/files/icons";
import { formatBytes } from "@/utils/size";

const settingsStore = useSettingsStore();
const queryCache = useQueryCache();
const { isDragging } = useExplorerDragSession();

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
  isEncrypted: props.data.currentVersion.isEncrypted,
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

// Rows a drag passes over are not being inspected, so they warm nothing.
const prefetchDetails = useDebounceFn(() => {
  if (isDragging.value) return;

  queryCache.refresh(queryCache.ensure(getFile(props.data.fileId)));
  queryCache.refresh(queryCache.ensure(getTagsForFile(props.data.fileId)));

  // Encrypted versions have no backend preview, so warming that query would
  // only spend a doomed request.
  if (!props.data.currentVersion.isEncrypted) {
    const { fileId } = props.data;
    const versionId = props.data.currentVersion.id;

    queryCache.refresh(
      queryCache.ensure(
        getPreviewByVersion({
          fileId,
          versionId,
          mimeType: props.data.mimeType,
          cached: queryCache.getQueryData<PreviewResultDto>(
            FILES_QUERY_KEYS.previewByVersion(fileId, versionId),
          ),
        }),
      ),
    );
  }

  queryCache.refresh(
    queryCache.ensure(getVersionsForFile({ id: props.data.fileId, page: 1, pageSize: 10 })),
  );
}, 150);

// These requests only warm the details drawer, which fetches them for real on
// open. A pending warmup is cancelled as soon as interest moves away
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
