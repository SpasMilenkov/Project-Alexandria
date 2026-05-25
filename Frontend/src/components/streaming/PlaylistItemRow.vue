<template>
  <div
    :draggable="true"
    class="group flex items-center gap-3 px-4 py-3 bg-white/40 dark:bg-white/3 transition-colors select-none"
    :class="{
      'opacity-40': isDragging,
      'bg-primary/5 dark:bg-primary/10': isDragOver,
      'hover:bg-white/60 dark:hover:bg-white/6': !isDragging,
    }"
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

    <!-- Position number hides on hover, play button takes its place -->
    <div class="w-5 shrink-0 flex items-center justify-center">
      <span class="text-xs text-muted text-right group-hover:hidden">
        {{ item.position + 1 }}
      </span>
      <button
        class="hidden group-hover:flex items-center justify-center text-gray-500 dark:text-white/50 hover:text-primary dark:hover:text-primary transition-colors"
        @click.stop="emit('play')"
      >
        <UIcon name="mdi:play" class="w-4 h-4" />
      </button>
    </div>

    <UIcon
      :name="isVideo ? 'mdi:file-video' : 'mdi:music-note'"
      class="w-4 h-4 text-muted shrink-0"
    />

    <div class="flex-1 min-w-0">
      <p class="text-sm font-medium text-highlighted truncate">{{ item.fileName }}</p>
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

const props = defineProps<{
  item: PlaylistItemResponse;
  draggedItemId: string | null;
}>();

const emit = defineEmits<{
  remove: [];
  play: [];
  dragStart: [id: string];
  dragEnd: [];
  dragOver: [id: string];
  drop: [targetId: string];
}>();

const isVideo = computed(() => props.item.mimeType.startsWith("video/"));
const isDragging = computed(() => props.draggedItemId === props.item.id);
const isDragOver = ref(false);

function onDragStart(e: DragEvent) {
  e.dataTransfer?.setData("text/plain", props.item.id);
  emit("dragStart", props.item.id);
}

function onDragEnd() {
  isDragOver.value = false;
  emit("dragEnd");
}

function onDragOver() {
  if (props.draggedItemId !== props.item.id) {
    isDragOver.value = true;
    emit("dragOver", props.item.id);
  }
}

function onDragLeave() {
  isDragOver.value = false;
}

function onDrop() {
  isDragOver.value = false;
  emit("drop", props.item.id);
}
</script>
