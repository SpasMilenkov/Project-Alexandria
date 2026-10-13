<script setup lang="ts">
import { computed } from "vue";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useSettingsStore } from "@/stores/settings";

const { fileId } = defineProps<{ fileId: string }>();

const settings = useSettingsStore();
const thumbnail = useFileThumbnail(() => ({ fileId }));
const showArtwork = computed(() => settings.thumbnailsEnabled && !thumbnail.thumbnailErrored.value);
</script>

<template>
  <span
    class="relative flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-gray-200/70 bg-gray-100 dark:border-gray-700/70 dark:bg-gray-800"
    aria-hidden="true"
  >
    <UIcon
      v-if="!showArtwork || !thumbnail.thumbnailLoaded.value"
      name="i-mdi-play-circle-outline"
      class="size-5 text-gray-500 dark:text-gray-500"
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
