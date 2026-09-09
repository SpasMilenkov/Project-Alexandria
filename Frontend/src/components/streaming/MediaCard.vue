<script setup lang="ts">
import type { ContextMenuItem } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, onUnmounted, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

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

// Version-scoped thumbnail URL via the shared composable: browser + nginx
// cache the bytes, and the first <img> error per file heals the session with
// one shared refresh before retrying once (see useFileThumbnail).
const { thumbnailUrl, thumbnailLoaded, thumbnailErrored, onThumbnailLoad, onThumbnailError } =
  useFileThumbnail(() => ({
    fileId: file.fileId,
    versionId: file.currentVersionId,
  }));

const showSpinner = computed(() => !thumbnailLoaded.value && !thumbnailErrored.value);
const showFallbackIcon = computed(() => thumbnailErrored.value);

// Queue state
const queueIndex = computed(() => userQueue.value.findIndex((f) => f.fileId === file.fileId));
const isQueued = computed(() => queueIndex.value !== -1);

// Border/background follows selection first, then the now-playing state.
const gridBorderClass = computed(() => {
  if (selected) return "border-primary/60 ring-1 ring-primary/40";
  if (isActive.value) return "border-primary/50 ring-1 ring-primary/20";
  return "border-black/[0.08] dark:border-white/[0.08] hover:border-black/[0.15] dark:hover:border-white/[0.16]";
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
</script>

<template>
  <!-- GRID VIEW -->
  <UContextMenu v-if="viewMode === 'grid'" :items="contextItems" class="block w-full">
    <button
      class="group relative w-full text-left rounded-xl overflow-hidden border transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60"
      :class="gridBorderClass"
      @click="onCardClick($event)"
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
          :src="thumbnailUrl"
          :alt="displayName"
          class="w-full h-full object-cover transition-transform duration-300 group-hover:scale-[1.04]"
          :class="{ 'opacity-0': showSpinner }"
          @load="onThumbnailLoad"
          @error="onThumbnailError"
        />
        <div v-if="showFallbackIcon" class="w-full h-full flex items-center justify-center">
          <Icon :icon="typeIcon" class="w-8 h-8 text-gray-300 dark:text-white/15" />
        </div>

        <div
          class="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent pointer-events-none"
        />

        <span
          class="absolute top-2 flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[0.625rem] font-semibold tracking-wide bg-black/40 frosted-glass text-white/80 transition-all duration-200"
          :class="checkboxVisible ? 'left-9' : 'left-2 group-hover:left-9'"
        >
          <Icon :icon="typeIcon" class="w-3 h-3 flex-shrink-0" />
          {{ typeLabel }}
        </span>

        <span
          class="absolute top-2 left-2 transition-opacity duration-200 rounded-md bg-black/40 frosted-glass p-0.5"
          :class="checkboxVisible ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'"
          @click.stop="emit('toggle', file, $event)"
        >
          <UCheckbox
            :model-value="selected"
            tabindex="-1"
            aria-label="Select file"
            class="pointer-events-none"
          />
        </span>

        <div v-if="selected" class="absolute inset-0 bg-primary/20 pointer-events-none" />

        <div class="absolute top-2 right-2 flex items-center gap-1.5">
          <button
            v-if="isAudio"
            class="w-6 h-6 flex items-center justify-center rounded-md bg-black/40 frosted-glass text-white/70 hover:text-white transition-colors"
            aria-label="View audio analysis"
            @click.stop="emit('info', file)"
          >
            <Icon icon="mdi:information-outline" class="w-3.5 h-3.5" />
          </button>
          <span
            v-if="file.duration"
            class="px-1.5 py-0.5 rounded-md text-[0.625rem] font-medium bg-black/40 frosted-glass text-white/80 tabular-nums"
          >
            {{ formatDuration(file.duration) }}
          </span>
        </div>

        <div
          v-if="isVideo && !isActive"
          class="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none"
        >
          <div
            class="w-10 h-10 rounded-full frosted-glass glass-surface flex items-center justify-center"
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
          :src="thumbnailUrl"
          :alt="displayName"
          class="w-full h-full object-cover"
          @load="onThumbnailLoad"
          @error="onThumbnailError"
        />
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
