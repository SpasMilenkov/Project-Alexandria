<template>
  <div class="flex flex-col gap-2">
    <TextPreviewPane v-if="textPreview" :preview="textPreview" @download="downloadPreview" />

    <!--
      One fixed frame for every other file, so moving between results never
      shifts the details below. Pictures fit inside it whole: portrait pages and
      photos fill its height, landscape ones its width.
    -->
    <div
      v-else
      class="relative flex h-[min(24rem,50dvh)] w-full items-center justify-center overflow-hidden rounded-lg bg-elevated"
    >
      <img
        v-if="heldVisual"
        :src="heldVisual"
        alt=""
        class="absolute inset-0 h-full w-full object-contain"
      />
      <Icon
        v-else-if="!shown"
        :icon="fileIcon"
        class="h-12 w-12 text-gray-400 dark:text-gray-600"
        aria-hidden="true"
      />
      <img
        v-if="hasThumbnail && !isFullShown"
        :src="thumbnailUrl"
        alt=""
        class="absolute inset-0 h-full w-full object-contain"
        :class="{ 'opacity-0': !thumbnailLoaded }"
        @load="handleThumbnailLoad"
        @error="onThumbnailError"
      />
      <img
        v-if="fullSrc"
        :src="fullSrc"
        alt=""
        class="absolute inset-0 h-full w-full object-contain"
        :class="{ 'opacity-0': !isFullShown }"
        @load="handleFullLoad"
      />
    </div>

    <FileTooltipCard :data="file" />
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, ref, watch, watchEffect } from "vue";

import { type FileResult, type PreviewResultDto } from "@/api/file";
import { useFileDownload } from "@/composables/useFileDownload";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useLoadHold } from "@/composables/useLoadHold";
import { FILES_QUERY_KEYS, getPreviewByVersion } from "@/queries/files";
import { getFileIcon } from "@/utils/files/icons";

import FileTooltipCard from "./FileTooltipCard.vue";
import TextPreviewPane from "./TextPreviewPane.vue";

const { file } = defineProps<{ file: FileResult }>();

const queryCache = useQueryCache();
const { downloadVersion } = useFileDownload();

const fileIcon = computed(() => getFileIcon(file.fileName));

const isProtected = computed(
  () => file.currentVersion.isEncrypted || file.currentVersion.isPasswordProtected,
);

// Image thumbnails are small padded squares, so a photo stays small and soft in
// the frame. Images swap to the full preview once it loads; documents and videos
// keep their thumbnails, which already have the original proportions.
const isImage = computed(() => file.mimeType.startsWith("image/") && !isProtected.value);

// Thumbnails are version-scoped and usually already cached by the explorer grid,
// which keeps hovering cheap. Media players and PDF embeds stay in the details
// drawer, since loading them on every hover would be wasted work.
const {
  canHaveThumbnail,
  thumbnailErrored,
  thumbnailLoaded,
  thumbnailUrl,
  onThumbnailError,
  onThumbnailLoad,
} = useFileThumbnail(() => ({
  coverUpdatedAt: file.currentVersion.coverUpdatedAt,
  fileId: file.fileId,
  isEncrypted: file.currentVersion.isEncrypted,
  isPasswordProtected: file.currentVersion.isPasswordProtected,
  mimeType: file.mimeType,
  versionId: file.currentVersion.id,
}));

const hasThumbnail = computed(() => canHaveThumbnail.value && !thumbnailErrored.value);

// The preview carries the text excerpt for files without a thumbnail and the
// full picture for images.
const isPreviewEnabled = computed(
  () => !isProtected.value && (!canHaveThumbnail.value || isImage.value),
);

const { data: previewData, isPlaceholderData } = useQuery(() => ({
  ...getPreviewByVersion({
    cached: queryCache.getQueryData<PreviewResultDto>(
      FILES_QUERY_KEYS.previewByVersion(file.fileId, file.currentVersion.id),
    ),
    fileId: file.fileId,
    mimeType: file.mimeType,
    versionId: file.currentVersion.id,
  }),
  enabled: isPreviewEnabled.value,
  placeholderData: (prev) => prev,
}));

const fullSrc = computed(() => {
  if (!isImage.value || isPlaceholderData.value) {
    return null;
  }
  return previewData.value?.previewUrl ?? null;
});

// Switching files

const { isHolding, release } = useLoadHold(() => file.fileId);

// The best picture this file has shown, and the previous file's picture kept on
// screen while the next one loads. A file without a picture drops it straight away.
const shown = ref<string | null>(null);
const previous = ref<string | null>(null);

// Whether the excerpt is on screen, and whether it was for the previous file.
// The switch watcher is created first, so it reads the old value before the
// effect below updates it for the new file.
const showsText = ref(false);
const previousShowedText = ref(false);

watch(
  () => file.fileId,
  () => {
    previous.value = shown.value;
    shown.value = null;
    previousShowedText.value = showsText.value;
  },
);

const heldVisual = computed(() => {
  if (!isHolding.value || shown.value || !hasThumbnail.value) {
    return null;
  }
  return previous.value;
});

const isFullShown = computed(() => shown.value !== null && shown.value === fullSrc.value);

const handleThumbnailLoad = () => {
  onThumbnailLoad();
  if (!isFullShown.value) {
    shown.value = thumbnailUrl.value;
  }
  release();
};

const handleFullLoad = () => {
  const src = fullSrc.value;
  if (src) {
    shown.value = src;
    release();
  }
};

// A previous excerpt stands in only while it is held and only right after
// another excerpt, never for a file that turns out to have none.
const textPreview = computed(() => {
  if (canHaveThumbnail.value || !isPreviewEnabled.value) {
    return null;
  }
  if (isPlaceholderData.value && !(isHolding.value && previousShowedText.value)) {
    return null;
  }
  return previewData.value?.textPreview ?? null;
});

watchEffect(() => {
  showsText.value = textPreview.value !== null;
});

const downloadPreview = () => {
  void downloadVersion(file.currentVersion.id);
};
</script>
