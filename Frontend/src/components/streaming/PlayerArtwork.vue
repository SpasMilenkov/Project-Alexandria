<template>
  <div class="relative h-full w-full" data-player-artwork>
    <div class="absolute inset-0 flex items-center justify-center">
      <Icon icon="mdi:music-note" :class="iconClass" />
    </div>
    <img
      v-if="showImage"
      :src="thumbnailUrl"
      :alt="alt"
      class="absolute inset-0 h-full w-full object-cover"
      @load="onThumbnailLoad"
      @error="onThumbnailError"
    />
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";

const { file, alt = "", iconClass = "w-5 h-5 text-gray-400 dark:text-white/30" } =
  defineProps<{
    file: MediaFileDto | null;
    alt?: string;
    iconClass?: string;
  }>();

const { thumbnailUrl, thumbnailErrored, canHaveThumbnail, onThumbnailLoad, onThumbnailError } =
  useFileThumbnail(() => ({
    fileId: file?.fileId ?? "",
    versionId: file?.currentVersionId ?? "",
    mimeType: file?.mimeType ?? null,
  }));

const showImage = computed(
  () => file !== null && canHaveThumbnail.value && !thumbnailErrored.value,
);
</script>
