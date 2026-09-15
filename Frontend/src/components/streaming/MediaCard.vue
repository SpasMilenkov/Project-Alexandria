<script setup lang="ts">
import type { ContextMenuItem } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, onUnmounted, ref, useTemplateRef } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";
import { MEDIA_CARD_METADATA_HEIGHT } from "@/utils/media-grid-layout";

const {
  file,
  viewMode,
  selected = false,
  selectionMode = false,
} = defineProps<{
  file: MediaFileDto;
  viewMode: "grid" | "list";
  selected?: boolean;
  selectionMode?: boolean;
}>();

const store = usePlayerStore();
const { activeFile, userQueue } = storeToRefs(store);

const isActive = computed(() => activeFile.value?.fileId === file.fileId);
const isAudio = computed(() => !file.isVideo);
const isVideo = computed(() => file.isVideo);
const typeLabel = computed(() => (isVideo.value ? "Video" : "Audio"));
const typeIcon = computed(() => (isVideo.value ? "mdi:file-video" : "mdi:music-note"));

const displayName = computed(() => file.title ?? file.fileName);
const subtitle = computed(() => file.artist ?? file.mimeType);
const gridSubtitle = computed(
  () => file.artist || file.album || (isVideo.value ? "Video" : "Unknown artist"),
);

// Version-scoped URLs preserve caching while the shared composable handles retries.
const { thumbnailUrl, thumbnailLoaded, thumbnailErrored, onThumbnailLoad, onThumbnailError } =
  useFileThumbnail(() => ({
    fileId: file.fileId,
    versionId: file.currentVersionId,
  }));

const showSpinner = computed(() => !thumbnailLoaded.value && !thumbnailErrored.value);
const showFallbackIcon = computed(() => thumbnailErrored.value);
const thumbnailImage = useTemplateRef<HTMLImageElement>("thumbnailImage");

// Recycled rows can receive late events from a detached image's pending request.
const isCurrentThumbnail = (event: Event) =>
  event.target === thumbnailImage.value &&
  thumbnailImage.value?.getAttribute("src") === thumbnailUrl.value;

const onArtworkLoad = (event: Event) => {
  if (isCurrentThumbnail(event)) onThumbnailLoad();
};

const onArtworkError = (event: Event) => {
  if (isCurrentThumbnail(event)) onThumbnailError();
};

// Queue state
const queueIndex = computed(() => userQueue.value.findIndex((f) => f.fileId === file.fileId));
const isQueued = computed(() => queueIndex.value !== -1);

// Border/background follows selection first, then the now-playing state.
const gridBorderClass = computed(() => {
  if (selected) return "border-primary/60 ring-1 ring-primary/40";
  if (isActive.value) return "border-primary/50 ring-1 ring-primary/20";
  return "border-gray-200/70 dark:border-gray-700/70 hover:border-gray-400 dark:hover:border-gray-500";
});

const listRowClass = computed(() => {
  if (selected) return "bg-primary/[0.08] dark:bg-primary/[0.1]";
  if (isActive.value) return "bg-primary/[0.06] dark:bg-primary/[0.08]";
  return "hover:bg-black/[0.025] dark:hover:bg-white/[0.03]";
});

const checkboxVisible = computed(() => selectionMode || selected);

// Context menu
const contextItems = computed((): ContextMenuItem[][] => [
  [
    {
      label: "Play now",
      icon: "i-mdi-play",
      onSelect: () => emit("select", file),
    },
  ],
  [
    isQueued.value
      ? {
          label: "Remove from queue",
          icon: "i-mdi-playlist-minus",
          onSelect: () => store.dequeueAt(queueIndex.value),
        }
      : {
          label: "Add to queue",
          icon: "i-mdi-playlist-plus",
          onSelect: () => store.enqueue(file),
        },
  ],
]);

const emit = defineEmits<{
  info: [file: MediaFileDto];
  select: [file: MediaFileDto];
  toggle: [file: MediaFileDto, event: MouseEvent];
  longpress: [file: MediaFileDto];
}>();

// Long-press enters selection mode. The press timer loses to any scroll/drag
// movement, and clicks landing right after a long-press are swallowed so the
// release tap does not immediately toggle the card back.
const LONG_PRESS_MS = 400;
const LONG_PRESS_MOVE_PX = 10;
const pressTimer = ref<ReturnType<typeof setTimeout> | null>(null);
const pressOrigin = ref<{ x: number; y: number } | null>(null);
const lastLongPressAt = ref(0);

const clearPressTimer = () => {
  if (pressTimer.value !== null) {
    clearTimeout(pressTimer.value);
    pressTimer.value = null;
  }
  pressOrigin.value = null;
};

onUnmounted(clearPressTimer);

const onPressStart = (event: PointerEvent) => {
  if (event.pointerType === "mouse" && event.button !== 0) return;
  clearPressTimer();
  pressOrigin.value = { x: event.clientX, y: event.clientY };
  pressTimer.value = setTimeout(() => {
    pressTimer.value = null;
    pressOrigin.value = null;
    lastLongPressAt.value = Date.now();
    if (navigator.vibrate) navigator.vibrate(10);
    emit("longpress", file);
  }, LONG_PRESS_MS);
};

const onPressMove = (event: PointerEvent) => {
  if (!pressOrigin.value) return;
  const dx = event.clientX - pressOrigin.value.x;
  const dy = event.clientY - pressOrigin.value.y;
  if (Math.hypot(dx, dy) > LONG_PRESS_MOVE_PX) clearPressTimer();
};

const onCardClick = (event: MouseEvent) => {
  if (Date.now() - lastLongPressAt.value < 500) return;
  if (selectionMode || event.ctrlKey || event.metaKey) emit("toggle", file, event);
  else emit("select", file);
};

const onCardKeydown = (event: KeyboardEvent) => {
  if (event.repeat) return;
  onCardClick(
    new MouseEvent("click", {
      ctrlKey: event.ctrlKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey,
    }),
  );
};
</script>

<template>
  <!-- GRID VIEW -->
  <UContextMenu v-if="viewMode === 'grid'" :items="contextItems" class="block w-full min-w-0">
    <div
      role="button"
      tabindex="0"
      :aria-label="`${selectionMode ? 'Select' : 'Play'} ${displayName}`"
      class="group relative w-full cursor-pointer text-left rounded-xl overflow-hidden border transition-colors duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary"
      :class="gridBorderClass"
      @click="onCardClick($event)"
      @keydown.enter.self.prevent="onCardKeydown"
      @keydown.space.self.prevent="onCardKeydown"
      @pointerdown="onPressStart"
      @pointermove="onPressMove"
      @pointerup="clearPressTimer"
      @pointerleave="clearPressTimer"
      @pointercancel="clearPressTimer"
    >
      <div
        class="w-full relative overflow-hidden bg-gray-100 dark:bg-neutral-900"
        :class="isAudio ? 'aspect-square' : 'aspect-video'"
      >
        <img
          v-if="!showFallbackIcon"
          :key="thumbnailUrl"
          ref="thumbnailImage"
          :src="thumbnailUrl"
          :alt="displayName"
          class="absolute inset-0 w-full h-full object-cover"
          :class="{ 'opacity-0': showSpinner }"
          @load="onArtworkLoad"
          @error="onArtworkError"
        />
        <div
          v-if="showSpinner"
          class="absolute inset-0 flex items-center justify-center"
          role="status"
          aria-label="Loading artwork"
        >
          <Icon icon="mdi:loading" class="w-5 h-5 animate-spin text-gray-600 dark:text-gray-400" />
        </div>
        <div v-if="showFallbackIcon" class="absolute inset-0 flex items-center justify-center">
          <Icon :icon="typeIcon" class="w-8 h-8 text-gray-500 dark:text-gray-400" />
        </div>

        <div
          class="absolute top-2 left-2 rounded-lg p-2 frosted-glass glass-surface-strong transition-opacity duration-150"
          :class="{
            'md:opacity-0 group-hover:opacity-100 group-focus-within:opacity-100': !checkboxVisible,
          }"
          @pointerdown.stop
          @click.stop="emit('toggle', file, $event)"
        >
          <UCheckbox :model-value="selected" :aria-label="`Select ${displayName}`" />
        </div>

        <div
          v-if="!isActive && !selectionMode"
          class="absolute bottom-4 right-4 w-10 h-10 rounded-full frosted-glass glass-surface-strong flex items-center justify-center opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100 transition-opacity duration-150 pointer-events-none"
          aria-hidden="true"
        >
          <Icon icon="mdi:play" class="w-5 h-5 text-gray-900 dark:text-gray-100" />
        </div>
      </div>

      <div
        class="p-4 flex flex-col gap-2 frosted-glass glass-surface"
        :style="{ height: `${MEDIA_CARD_METADATA_HEIGHT}px` }"
      >
        <div class="min-w-0">
          <p
            class="text-sm leading-5 font-semibold truncate m-0"
            :class="isActive ? 'text-primary' : 'text-gray-900 dark:text-gray-100'"
            :title="displayName"
          >
            {{ displayName }}
          </p>
          <p
            class="text-xs leading-5 text-gray-600 dark:text-gray-400 truncate m-0"
            :title="gridSubtitle"
          >
            {{ gridSubtitle }}
          </p>
        </div>
        <div class="flex items-center justify-between gap-2 h-8 shrink-0">
          <div class="flex items-center gap-2 min-w-0">
            <div
              v-if="isActive"
              class="flex gap-0.5 items-end h-3.5"
              role="status"
              aria-label="Now playing"
            >
              <div class="w-0.5 bg-primary rounded-full animate-eq-1" />
              <div class="w-0.5 bg-primary rounded-full animate-eq-2" />
              <div class="w-0.5 bg-primary rounded-full animate-eq-3" />
            </div>
            <span
              v-if="file.duration"
              class="text-xs tabular-nums text-gray-600 dark:text-gray-400"
            >
              {{ formatDuration(file.duration) }}
            </span>
          </div>
          <button
            v-if="isAudio"
            type="button"
            class="w-8 h-8 shrink-0 flex items-center justify-center rounded-lg text-gray-600 dark:text-gray-400 hover:bg-black/5 dark:hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            aria-label="View audio analysis"
            title="View audio analysis"
            @pointerdown.stop
            @click.stop="emit('info', file)"
          >
            <Icon icon="mdi:information-outline" class="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  </UContextMenu>

  <!-- LIST VIEW -->
  <UContextMenu v-else :items="contextItems" class="block w-full">
    <button
      class="group w-full text-left flex items-center gap-3 px-3 py-2.5 transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60"
      :class="listRowClass"
      @click="onCardClick($event)"
      @pointerdown="onPressStart"
      @pointermove="onPressMove"
      @pointerup="clearPressTimer"
      @pointerleave="clearPressTimer"
      @pointercancel="clearPressTimer"
    >
      <div
        class="w-9 h-9 rounded-lg overflow-hidden bg-gray-100 dark:bg-neutral-900 flex-shrink-0 relative"
      >
        <img
          v-if="!showFallbackIcon"
          :key="thumbnailUrl"
          ref="thumbnailImage"
          :src="thumbnailUrl"
          :alt="displayName"
          class="w-full h-full object-cover"
          :class="{ 'opacity-0': showSpinner }"
          @load="onArtworkLoad"
          @error="onArtworkError"
        />
        <div
          v-if="showSpinner"
          class="absolute inset-0 flex items-center justify-center"
          role="status"
          aria-label="Loading artwork"
        >
          <Icon icon="mdi:loading" class="w-4 h-4 animate-spin text-gray-600 dark:text-gray-400" />
        </div>
        <div v-if="showFallbackIcon" class="w-full h-full flex items-center justify-center">
          <Icon :icon="typeIcon" class="w-3.5 h-3.5 text-gray-300 dark:text-white/15" />
        </div>
      </div>

      <div class="flex-1 min-w-0">
        <div
          class="text-sm font-medium truncate leading-snug m-0 flex items-center gap-1.5"
          :class="isActive ? 'text-primary dark:text-primary' : 'text-gray-800 dark:text-white/85'"
        >
          <div class="w-5 flex-shrink-0 flex justify-center">
            <span v-if="checkboxVisible" @click.stop="emit('toggle', file, $event)">
              <UCheckbox
                :model-value="selected"
                tabindex="-1"
                aria-label="Select file"
                class="pointer-events-none"
              />
            </span>
            <div v-else-if="isActive" class="flex gap-0.5 items-end h-3.5">
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
        </div>
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
          {{ formatDuration(file.duration) }}
        </span>
        <button
          v-if="isAudio"
          class="p-1.5 rounded-md text-gray-400 dark:text-white/35 hover:text-gray-600 dark:hover:text-white/70 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] transition-colors"
          aria-label="View audio analysis"
          @click.stop="emit('info', file)"
        >
          <Icon icon="mdi:information-outline" class="w-4 h-4" />
        </button>
      </div>
    </button>
  </UContextMenu>
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
