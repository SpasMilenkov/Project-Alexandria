<template>
  <li
    class="flex items-center gap-3 px-3 py-2 hover:bg-black/[0.03] dark:hover:bg-white/[0.04] transition-colors duration-100 group"
  >
    <!-- Thumbnail -->
    <div
      class="w-9 h-9 rounded-lg shrink-0 overflow-hidden bg-black dark:bg-white flex items-center justify-center"
    >
      <template v-if="showSpinner">
        <BlockSpinner />
      </template>
      <img
        v-else-if="loadedSrc"
        :src="loadedSrc"
        :alt="file.title ?? file.fileName"
        class="w-full h-full object-cover"
      />
      <Icon
        v-else
        :icon="mediaType === 'video' ? 'mdi:film-outline' : 'mdi:music-note-outline'"
        class="w-4 h-4 text-gray-400 dark:text-white/30"
      />
      <!-- Preload img — invisible, just triggers onload -->
      <img
        v-if="thumbnail && loadedSrc !== thumbnail"
        :src="thumbnail"
        class="hidden"
        @load="loadedSrc = thumbnail"
      />
    </div>

    <!-- Title + duration -->
    <div class="flex-1 min-w-0">
      <p class="text-sm font-medium text-gray-700 dark:text-white/80 truncate m-0 leading-snug">
        {{ file.title ?? file.fileName }}
      </p>
      <p
        v-if="file.duration"
        class="text-[11px] text-gray-400 dark:text-white/30 m-0 leading-snug tabular-nums"
      >
        {{ formatDuration(file.duration) }}
      </p>
    </div>

    <!-- Actions (visible on hover) -->
    <div
      class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity duration-150 shrink-0"
    >
      <UTooltip text="Play now">
        <UButton
          size="xs"
          variant="ghost"
          color="primary"
          icon="i-mdi-play"
          @click="emit('play-now', file)"
        />
      </UTooltip>
      <UTooltip text="Add to queue">
        <UButton
          size="xs"
          variant="ghost"
          color="neutral"
          icon="i-heroicons-plus"
          @click="emit('enqueue', file)"
        />
      </UTooltip>
    </div>
  </li>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import { fileApi } from "@/api/file";
import { type MediaFileDto } from "@/api/streaming";
import { getPreview } from "@/queries/files";
import { formatDuration } from "@/utils/date-formatters";

import BlockSpinner from "../common/BlockSpinner.vue";

// Props & emits

const { file, mediaType } = defineProps<{
  file: MediaFileDto;
  mediaType: "video" | "audio";
}>();

const emit = defineEmits<{
  "play-now": [file: MediaFileDto];
  enqueue: [file: MediaFileDto];
}>();

const thumbnail = computed(() =>
  fileApi.getThumbnailUrlForVersion(file.fileId, file.currentVersionId),
);

const loadedSrc = ref<string | null>(null);

const showSpinner = computed(() => Boolean(thumbnail.value) && loadedSrc.value !== thumbnail.value);
</script>
