<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { FileResult } from "@/api/file";

import { useHomeWidgetThumbnail } from "@/composables/useHomeWidgetPreview";
import { useSettingsStore } from "@/stores/settings";
import { homeFileVisual } from "@/utils/home-file-widgets";

const { file, cover = false } = defineProps<{ file: FileResult; cover?: boolean }>();

const settings = useSettingsStore();

const visual = computed(() => homeFileVisual(file));

const thumbnail = useHomeWidgetThumbnail(() => ({
  fileId: file.fileId,
  versionId: file.currentVersion.id,
  mimeType: file.mimeType,
}));

const showThumbnail = computed(
  () =>
    settings.thumbnailsEnabled &&
    !file.currentVersion.isEncrypted &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);
</script>

<template>
  <div
    class="relative flex min-h-0 min-w-0 flex-1 items-center justify-center overflow-hidden rounded-lg bg-gray-100 dark:bg-gray-800"
  >
    <img
      v-if="showThumbnail"
      :src="thumbnail.thumbnailUrl.value"
      alt=""
      loading="lazy"
      decoding="async"
      class="absolute inset-0 h-full w-full"
      :class="{
        invisible: !thumbnail.thumbnailLoaded.value,
        'object-cover': cover,
        'object-contain': !cover,
      }"
      @load="thumbnail.onThumbnailLoad"
      @error="thumbnail.onThumbnailError"
    />

    <Icon
      v-if="!showThumbnail || !thumbnail.thumbnailLoaded.value"
      :icon="visual.icon"
      class="size-8"
      :style="{ color: visual.color }"
      aria-hidden="true"
    />
  </div>
</template>
