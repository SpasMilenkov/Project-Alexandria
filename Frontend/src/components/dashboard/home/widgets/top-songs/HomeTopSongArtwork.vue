<script setup lang="ts">
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useHomeWidgetThumbnail } from "@/composables/useHomeWidgetPreview";
import { useSettingsStore } from "@/stores/settings";

const { file } = defineProps<{ file: MediaFileDto }>();

const settings = useSettingsStore();

const thumbnail = useHomeWidgetThumbnail(() => ({
  fileId: file.fileId,
  versionId: file.playbackVersionId ?? file.currentVersionId,
  mimeType: file.mimeType,
}));

const showArtwork = computed(
  () =>
    settings.thumbnailsEnabled &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);
</script>

<template>
  <span
    class="relative flex size-8 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-gray-100 dark:bg-gray-800"
  >
    <UIcon
      v-if="!showArtwork || !thumbnail.thumbnailLoaded.value"
      name="i-mdi-music-note-outline"
      class="size-4 text-gray-500 dark:text-gray-500"
      aria-hidden="true"
    />

    <img
      v-if="showArtwork"
      :src="thumbnail.thumbnailUrl.value"
      alt=""
      loading="lazy"
      decoding="async"
      class="absolute inset-0 size-full object-cover"
      :class="{ invisible: !thumbnail.thumbnailLoaded.value }"
      @load="thumbnail.onThumbnailLoad"
      @error="thumbnail.onThumbnailError"
    />
  </span>
</template>
