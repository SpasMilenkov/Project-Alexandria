<template>
  <div
    :draggable="true"
    class="group flex items-center gap-3 px-4 py-3 bg-white/40 dark:bg-white/3 transition-colors select-none"
    :class="{
      'opacity-40': isDragging,
      'bg-primary/5 dark:bg-primary/10': isDragOver || isPlaying,
      'hover:bg-white/60 dark:hover:bg-white/6': !isDragging && !isPlaying,
    }"
    :aria-current="isPlaying ? 'true' : undefined"
    @dragstart="onDragStart"
    @dragend="onDragEnd"
    @dragover.prevent="onDragOver"
    @dragleave="onDragLeave"
    @drop.prevent="onDrop"
    @click.stop="emit('play')"
  >
    <UIcon
      name="lucide:grip-vertical"
      class="w-4 h-4 text-muted cursor-grab active:cursor-grabbing shrink-0"
    />

    <!-- Position number hides on hover, play button takes its place.
         The now-playing track keeps the equalizer visible instead. -->
    <div class="w-5 shrink-0 flex items-center justify-center">
      <div
        v-if="isPlaying"
        class="flex gap-0.5 items-end h-3.5"
        role="status"
        aria-label="Now playing"
      >
        <div class="w-0.5 bg-primary rounded-full animate-eq-1" />
        <div class="w-0.5 bg-primary rounded-full animate-eq-2" />
        <div class="w-0.5 bg-primary rounded-full animate-eq-3" />
      </div>
      <template v-else>
        <span class="text-xs text-muted text-right group-hover:hidden">
          {{ item.position + 1 }}
        </span>
        <button
          class="hidden group-hover:flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-primary dark:hover:text-primary transition-colors"
          @click.stop="emit('play')"
        >
          <UIcon name="mdi:play" class="w-4 h-4" />
        </button>
      </template>
    </div>

    <div
      class="relative w-10 h-10 rounded-lg overflow-hidden shrink-0 bg-gray-100/80 dark:bg-white/5"
    >
      <img
        v-if="showThumbnail"
        :src="thumbUrl"
        :alt="item.fileName"
        loading="lazy"
        decoding="async"
        class="h-full w-full object-cover"
        @error="thumbErrored = true"
      />
      <div v-else class="flex h-full w-full items-center justify-center">
        <UIcon
          :name="isVideo ? 'mdi:file-video' : 'mdi:music-note'"
          class="w-4 h-4 text-muted shrink-0"
        />
      </div>
    </div>

    <div class="flex-1 min-w-0">
      <p
        class="text-sm font-medium truncate"
        :class="isPlaying ? 'text-primary' : 'text-highlighted'"
      >
        {{ item.fileName }}
      </p>
      <p class="text-xs text-muted truncate">{{ item.mimeType }}</p>
    </div>

    <UBadge
      :label="`${item.representations.length} rep${item.representations.length !== 1 ? 's' : ''}`"
      color="neutral"
      variant="subtle"
      size="sm"
      class="shrink-0"
    />

    <UButton
      icon="i-heroicons-x-mark"
      color="neutral"
      variant="ghost"
      size="xs"
      class="shrink-0"
      @click.stop="emit('remove')"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

import type { PlaylistItemResponse } from "@/api/playlist";

import { fileApi } from "@/api/file";
import { useSettingsStore } from "@/stores/settings";
import { isThumbnailSupportedMimeType } from "@/utils/mimetype.utils";

const { item, draggedItemId, isPlaying } = defineProps<{
  item: PlaylistItemResponse;
  draggedItemId: string | null;
  isPlaying?: boolean;
}>();

const emit = defineEmits<{
  remove: [];
  play: [];
  dragStart: [id: string];
  dragEnd: [];
  dragOver: [id: string];
  drop: [targetId: string];
}>();

const isVideo = computed(() => item.mimeType.startsWith("video/"));
const isDragging = computed(() => draggedItemId === item.id);
const isDragOver = ref(false);

const settingsStore = useSettingsStore();
const thumbErrored = ref(false);
const canHaveThumbnail = computed(() => isThumbnailSupportedMimeType(item.mimeType));
const showThumbnail = computed(
  () => settingsStore.thumbnailsEnabled && canHaveThumbnail.value && !thumbErrored.value,
);
const thumbUrl = computed(() => fileApi.getThumbnailUrl(item.fileId));

function onDragStart(e: DragEvent) {
  e.dataTransfer?.setData("text/plain", item.id);
  emit("dragStart", item.id);
}

function onDragEnd() {
  isDragOver.value = false;
  emit("dragEnd");
}

function onDragOver() {
  if (draggedItemId !== item.id) {
    isDragOver.value = true;
    emit("dragOver", item.id);
  }
}

function onDragLeave() {
  isDragOver.value = false;
}

function onDrop() {
  isDragOver.value = false;
  emit("drop", item.id);
}
</script>

<style scoped>
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
