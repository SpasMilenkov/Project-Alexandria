<template>
  <li>
    <div
      role="button"
      tabindex="0"
      :aria-label="`Play ${file.title ?? file.fileName}`"
      class="flex items-center cursor-pointer hover:bg-black/[0.03] dark:hover:bg-white/[0.04] transition-colors duration-100 group focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60"
      :class="sizeClasses.row"
      @click="playNow"
      @keydown.enter.self.prevent="playNow"
      @keydown.space.self.prevent="playNow"
    >
      <!-- Thumbnail -->
      <div
        class="shrink-0 overflow-hidden bg-black dark:bg-white flex items-center justify-center"
        :class="sizeClasses.thumb"
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
        <p
          class="font-medium text-gray-700 dark:text-white/80 truncate m-0 leading-snug"
          :class="sizeClasses.title"
        >
          {{ file.title ?? file.fileName }}
        </p>

        <p
          v-if="file.lyricsSnippet"
          class="flex items-center gap-1 text-xs italic text-gray-600 dark:text-gray-400 m-0 leading-snug min-w-0"
          :title="file.lyricsSnippet"
        >
          <Icon icon="mdi:microphone-variant" class="w-3 h-3 shrink-0" aria-hidden="true" />
          <span class="sr-only">Matched in lyrics:</span>
          <span class="truncate">{{ file.lyricsSnippet }}</span>
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
        class="flex items-center gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-150 shrink-0"
      >
        <UTooltip text="Play now">
          <UButton
            size="xs"
            variant="ghost"
            color="primary"
            icon="i-mdi-play"
            @click.stop="playNow"
          />
        </UTooltip>
        <UTooltip text="Add to queue">
          <UButton
            size="xs"
            variant="ghost"
            color="neutral"
            icon="i-heroicons-plus"
            @click.stop="emit('enqueue', file)"
          />
        </UTooltip>
      </div>
    </div>
  </li>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import { fileApi } from "@/api/file";
import { type MediaFileDto } from "@/api/streaming";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { getPreview } from "@/queries/files";
import { formatDuration } from "@/utils/date-formatters";

// Props & emits

const {
  file,
  mediaType,
  size = "md",
} = defineProps<{
  file: MediaFileDto;
  mediaType: "video" | "audio";
  size?: "md" | "lg";
}>();

const SIZE_CLASSES = {
  md: { row: "gap-3 px-3 py-2", thumb: "w-9 h-9 rounded-lg", title: "text-sm" },
  lg: { row: "gap-4 px-6 py-3", thumb: "size-12 rounded-xl", title: "text-base" },
} as const;

const sizeClasses = computed(() => SIZE_CLASSES[size]);

const emit = defineEmits<{
  "play-now": [file: MediaFileDto];
  enqueue: [file: MediaFileDto];
}>();

const playNow = () => {
  emit("play-now", file);
};

const thumbnail = computed(() =>
  fileApi.getThumbnailUrlForVersion(file.fileId, file.currentVersionId),
);

const loadedSrc = ref<string | null>(null);

const showSpinner = computed(() => Boolean(thumbnail.value) && loadedSrc.value !== thumbnail.value);
</script>
