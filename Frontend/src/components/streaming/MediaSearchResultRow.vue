<template>
  <li
    class="flex items-center gap-3 px-3 py-2 hover:bg-black/[0.03] dark:hover:bg-white/[0.04] transition-colors duration-100 group"
  >
    <!-- Thumbnail -->
    <div
      class="w-9 h-9 rounded-lg shrink-0 overflow-hidden bg-black dark:bg-white flex items-center justify-center"
    >
      <template v-if="showSpinner">
        <svg
          class="w-3.5 h-3.5 animate-spin text-gray-300 dark:text-white/20"
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            class="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            stroke-width="4"
          />
          <path
            class="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 22 6.477 22 12h-4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          />
        </svg>
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
          icon="i-heroicons-play"
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

import { type MediaFileDto } from "@/api/streaming";
import { getPreview } from "@/queries/files";
import { formatDuration } from "@/utils/date-formatters";

// ─── Props & emits ────────────────────────────────────────────────────────────

const { file, mediaType } = defineProps<{
  file: MediaFileDto;
  mediaType: "video" | "audio";
}>();

const emit = defineEmits<{
  "play-now": [file: MediaFileDto];
  enqueue: [file: MediaFileDto];
}>();

// ─── Thumbnail (presigned URL, fetched per-row) ───────────────────────────────

const { data: preview, isLoading: previewLoading } = useQuery(() => getPreview(file.fileId));

const thumbnail = computed(() => preview.value?.thumbnailUrl ?? null);
const loadedSrc = ref<string | null>(null);

const showSpinner = computed(
  () => previewLoading.value || (Boolean(thumbnail.value) && loadedSrc.value !== thumbnail.value),
);
</script>
