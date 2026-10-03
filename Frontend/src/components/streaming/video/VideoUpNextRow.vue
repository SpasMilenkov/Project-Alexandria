<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { formatDuration } from "@/utils/date-formatters";

const {
  file,
  active = false,
  playing = false,
  progressPercent = null,
  watched = false,
} = defineProps<{
  file: MediaFileDto;
  active?: boolean;
  playing?: boolean;
  progressPercent?: number | null;
  watched?: boolean;
}>();

const emit = defineEmits<{
  select: [file: MediaFileDto];
}>();

const { thumbnailUrl, thumbnailLoaded, thumbnailErrored, onThumbnailLoad, onThumbnailError } =
  useFileThumbnail(() => ({
    fileId: file.fileId,
    versionId: file.currentVersionId,
  }));

const showArtwork = computed(() => !thumbnailErrored.value);
const showSpinner = computed(() => !thumbnailLoaded.value && !thumbnailErrored.value);
const showProgress = computed(() => progressPercent !== null && progressPercent > 0 && !watched);

const displayName = computed(() => file.title ?? file.fileName);
const subtitle = computed(() => {
  if (watched) return "Watched";
  return file.artist ?? "Video";
});

const onSelect = () => emit("select", file);
</script>

<template>
  <button
    type="button"
    class="group w-full flex items-center gap-3 p-1.5 rounded-xl text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
    :class="active ? 'bg-primary/10' : 'hover:bg-black/5 dark:hover:bg-white/5'"
    :aria-label="`Play ${displayName}`"
    :aria-current="active ? 'true' : undefined"
    @click="onSelect"
  >
    <span
      class="relative block w-[104px] shrink-0 aspect-video rounded-lg overflow-hidden bg-gray-100 dark:bg-neutral-900"
    >
      <img
        v-if="showArtwork"
        :src="thumbnailUrl"
        :alt="displayName"
        loading="lazy"
        class="absolute inset-0 w-full h-full object-cover"
        :class="{ 'opacity-0': showSpinner }"
        @load="onThumbnailLoad"
        @error="onThumbnailError"
      />
      <span
        v-if="showSpinner"
        class="absolute inset-0 flex items-center justify-center"
        role="status"
        aria-label="Loading artwork"
      >
        <Icon icon="mdi:loading" class="w-4 h-4 animate-spin text-gray-600 dark:text-gray-400" />
      </span>
      <span
        v-if="thumbnailErrored"
        class="absolute inset-0 flex items-center justify-center"
        aria-hidden="true"
      >
        <Icon icon="mdi:file-video" class="w-6 h-6 text-gray-500 dark:text-gray-400" />
      </span>
      <span
        v-if="active && playing"
        class="absolute left-1.5 top-1.5 flex items-center justify-center w-7 h-6 rounded-md bg-black/65"
        role="status"
        aria-label="Now playing"
      >
        <Icon icon="mdi:eye-outline" class="w-4 h-4 text-white animate-blink" />
      </span>
      <span
        v-if="file.duration"
        class="absolute right-1.5 bottom-1.5 px-1.5 py-px rounded text-[11px] tabular-nums bg-black/70 text-white"
      >
        {{ formatDuration(file.duration) }}
      </span>
      <span
        v-if="showProgress"
        class="absolute left-0 right-0 bottom-0 h-[3px] bg-white/25"
        aria-hidden="true"
      >
        <span class="block h-full bg-primary" :style="{ width: `${progressPercent}%` }" />
      </span>
    </span>
    <span class="flex-1 min-w-0">
      <span
        class="block font-semibold text-[13px] leading-snug line-clamp-2"
        :class="active ? 'text-primary' : 'text-gray-900 dark:text-gray-100'"
      >
        {{ displayName }}
      </span>
      <span class="block text-xs text-gray-500 dark:text-gray-500 truncate mt-0.5">
        {{ subtitle }}
      </span>
    </span>
  </button>
</template>

<style scoped>
@keyframes blink-eye {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.25;
  }
}
.animate-blink {
  animation: blink-eye 1.2s ease-in-out infinite;
}

@media (prefers-reduced-motion: reduce) {
  .animate-blink {
    animation: none;
  }
}
</style>
