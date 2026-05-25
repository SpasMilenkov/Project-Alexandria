<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { getPreview } from "@/queries/files";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

const props = defineProps<{
  file: MediaFileDto;
  viewMode: "grid" | "list";
}>();

const store = usePlayerStore();
const { activeFile } = storeToRefs(store);

const isActive = computed(() => activeFile.value?.fileId === props.file.fileId);
const isAudio = computed(() => !props.file.isVideo);
const isVideo = computed(() => props.file.isVideo);
const typeLabel = computed(() => (isVideo.value ? "Video" : "Audio"));
const typeIcon = computed(() => (isVideo.value ? "mdi:file-video" : "mdi:music-note"));

const displayName = computed(() => props.file.title ?? props.file.fileName);
const subtitle = computed(() => props.file.artist ?? props.file.mimeType);

const { data: preview, isLoading: previewLoading } = useQuery(() => getPreview(props.file.fileId));
const thumbnail = computed(() => preview.value?.thumbnailUrl ?? null);

const loadedSrc = ref<string | null>(null);
const showSpinner = computed(
  () => previewLoading.value || (Boolean(thumbnail.value) && loadedSrc.value !== thumbnail.value),
);

const emit = defineEmits<{ select: [file: MediaFileDto] }>();

</script>

<template>
  <!-- GRID VIEW -->
  <button
    v-if="viewMode === 'grid'"
    class="group relative w-full text-left rounded-xl overflow-hidden border transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60"
    :class="
      isActive
        ? 'border-primary/50 ring-1 ring-primary/20'
        : 'border-black/[0.08] dark:border-white/[0.08] hover:border-black/[0.15] dark:hover:border-white/[0.16]'
    "
    @click="emit('select', file)"

  >
    <div
      class="w-full relative overflow-hidden bg-gray-100 dark:bg-gray-900"
      :class="isAudio ? 'aspect-square' : 'aspect-video'"
    >
      <img
        v-if="thumbnail"
        :src="thumbnail"
        :alt="displayName"
        class="w-full h-full object-cover transition-transform duration-300 group-hover:scale-[1.04]"
        :class="{ 'opacity-0': showSpinner }"
        @load="loadedSrc = thumbnail"
        @error="loadedSrc = thumbnail"
      />
      <div v-else-if="!previewLoading" class="w-full h-full flex items-center justify-center">
        <Icon :icon="typeIcon" class="w-8 h-8 text-gray-300 dark:text-white/15" />
      </div>

      <div
        class="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent pointer-events-none"
      />

      <span
        class="absolute top-2 left-2 flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[0.625rem] font-semibold tracking-wide bg-black/40 backdrop-blur-md text-white/80"
      >
        <Icon :icon="typeIcon" class="w-3 h-3 flex-shrink-0" />
        {{ typeLabel }}
      </span>

      <span
        v-if="file.duration"
        class="absolute top-2 right-2 px-1.5 py-0.5 rounded-md text-[0.625rem] font-medium bg-black/40 backdrop-blur-md text-white/80 tabular-nums"
      >
        {{ file.duration }}
      </span>

      <div
        v-if="isVideo && !isActive"
        class="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none"
      >
        <div
          class="w-10 h-10 rounded-full bg-white/20 backdrop-blur-sm flex items-center justify-center"
        >
          <Icon icon="mdi:play" class="w-5 h-5 text-white ml-0.5" />
        </div>
      </div>

      <Transition name="fade">
        <div
          v-if="isActive"
          class="absolute inset-0 bg-primary/25 flex items-center justify-center"
        >
          <AudioEqualizer />
        </div>
      </Transition>

      <div class="absolute bottom-0 left-0 right-0 px-3 pb-2.5 pt-8">
        <p class="text-[0.8125rem] font-semibold text-white/95 truncate leading-snug m-0">
          {{ displayName }}
        </p>
        <p class="text-[0.6875rem] text-white/45 mt-0.5 m-0 truncate">
          {{ subtitle }}
        </p>
      </div>
    </div>
  </button>

  <!-- LIST VIEW -->
  <button
    v-else
    class="group w-full text-left flex items-center gap-3 px-3 py-2.5 transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60"
    :class="
      isActive
        ? 'bg-primary/[0.06] dark:bg-primary/[0.08]'
        : 'hover:bg-black/[0.025] dark:hover:bg-white/[0.03]'
    "
    @click="store.setActiveFile(file)"
  >
    <div
      class="w-9 h-9 rounded-lg overflow-hidden bg-gray-100 dark:bg-gray-900 flex-shrink-0 relative"
    >
      <img
        v-if="thumbnail"
        :src="thumbnail"
        :alt="displayName"
        class="w-full h-full object-cover"
      />
      <div v-else class="w-full h-full flex items-center justify-center">
        <Icon :icon="typeIcon" class="w-3.5 h-3.5 text-gray-300 dark:text-white/15" />
      </div>


    </div>

    <div class="flex-1 min-w-0">
      <p
        class="text-sm font-medium truncate leading-snug m-0 flex items-center gap-1.5"
        :class="isActive ? 'text-primary dark:text-primary' : 'text-gray-800 dark:text-white/85'"
      >
        <div class="w-5 flex-shrink-0 flex justify-center">
          <div v-if="isActive" class="flex gap-0.5 items-end h-3.5">
            <div class="w-0.5 bg-primary rounded-full animate-eq-1"></div>
            <div class="w-0.5 bg-primary rounded-full animate-eq-2"></div>
            <div class="w-0.5 bg-primary rounded-full animate-eq-3"></div>
          </div>
          <svg
            v-else
            class="w-4 h-4 text-gray-600 group-hover:text-gray-400"
            fill="currentColor"
            viewBox="0 0 24 24"
          >
            <path d="M8 5v14l11-7z" />
          </svg>
        </div>
        {{ displayName }}
      </p>
      <p class="text-xs text-gray-400 dark:text-white/30 truncate m-0 mt-0.5">
        {{ subtitle }}
      </p>
    </div>

    <div class="flex items-center gap-2.5 flex-shrink-0">
      <span
        class="hidden sm:inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[0.625rem] font-medium bg-black/[0.04] dark:bg-white/[0.06] text-gray-500 dark:text-white/40"
      >
        <Icon :icon="typeIcon" class="w-3 h-3" />
        {{ typeLabel }}
      </span>
      <span
        v-if="file.duration"
        class="text-xs tabular-nums text-gray-400 dark:text-white/35 min-w-[2.5rem] text-right"
      >
        {{ formatDuration(file.duration)  }}
      </span>
    </div>
  </button>
</template>


<style scoped>
.fade-enter-active {
  transition: opacity 180ms ease-out;
}
.fade-leave-active {
  transition: opacity 120ms ease-in;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@keyframes eq1 {
  0%,
  100% {
    height: 4px;
  }
  50% {
    height: 14px;
  }
}
@keyframes eq2 {
  0%,
  100% {
    height: 10px;
  }
  50% {
    height: 4px;
  }
}
@keyframes eq3 {
  0%,
  100% {
    height: 6px;
  }
  50% {
    height: 12px;
  }
}
.animate-eq-1 {
  animation: eq1 0.8s ease-in-out infinite;
}
.animate-eq-2 {
  animation: eq2 0.8s ease-in-out infinite 0.2s;
}
.animate-eq-3 {
  animation: eq3 0.8s ease-in-out infinite 0.4s;
}
</style>
