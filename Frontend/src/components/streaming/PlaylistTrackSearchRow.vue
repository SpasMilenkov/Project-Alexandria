<template>
  <div class="flex items-center gap-3 px-3 py-2.5 bg-white/40 dark:bg-white/3">
    <div
      class="relative w-9 h-9 rounded-md bg-gray-100/60 dark:bg-gray-800/40 flex items-center justify-center overflow-hidden shrink-0"
    >
      <UIcon
        v-if="!showArtwork || !thumbnail.thumbnailLoaded.value"
        name="mdi:music-note"
        class="w-4 h-4 text-muted"
      />

      <img
        v-if="showArtwork"
        :src="thumbnail.thumbnailUrl.value"
        alt=""
        loading="lazy"
        decoding="async"
        class="absolute inset-0 w-full h-full object-cover"
        :class="{ invisible: !thumbnail.thumbnailLoaded.value }"
        @load="thumbnail.onThumbnailLoad"
        @error="thumbnail.onThumbnailError"
      />
    </div>

    <div class="flex-1 min-w-0">
      <p class="text-sm font-medium text-highlighted truncate">
        {{ file.title ?? file.fileName }}
      </p>
      <p v-if="file.artist" class="text-xs text-muted truncate">{{ file.artist }}</p>
    </div>

    <UButton
      :icon="isAdded ? 'mdi:check' : 'i-heroicons-plus'"
      color="primary"
      variant="ghost"
      size="xs"
      :loading="isAdding"
      :disabled="isAdded"
      class="shrink-0"
      @click="emit('add', file.transpilationJobId)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useSettingsStore } from "@/stores/settings";

const { file, isAdding, isAdded } = defineProps<{
  file: MediaFileDto;
  isAdding: boolean;
  isAdded: boolean;
}>();

const emit = defineEmits<{
  add: [transpilationJobId: string];
}>();

const settings = useSettingsStore();

const thumbnail = useFileThumbnail(() => ({
  fileId: file.fileId,
  versionId: file.currentVersionId,
  mimeType: file.mimeType,
}));

const showArtwork = computed(
  () =>
    settings.thumbnailsEnabled &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);
</script>
