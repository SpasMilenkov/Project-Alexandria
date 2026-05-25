// oxlint-disable max-lines-per-function
// oxlint-disable max-statements
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

export const usePlayerStore = defineStore(
  "player",
  () => {
    const activeFile = ref<MediaFileDto | null>(null);
    const snapCorner = ref<"tl" | "tr" | "bl" | "br">("br");
    const volume = ref(0.5);
    const queue = ref<MediaFileDto[]>([]);
    const currentIndex = ref(-1);
    const autoplay = ref(true);
    const playerMode = ref<"expanded" | "pip" | "strip">("pip");
    const activePlaylistId = ref<string | null>(null);
    const loop = ref(false);
    const shuffled = ref(false);

    const hasNext = computed(() => currentIndex.value < queue.value.length - 1);
    const hasPrevious = computed(() => currentIndex.value > 0);
    const hasActiveFile = computed(() => activeFile.value !== null);
    const isAudio = computed(() => activeFile.value?.mimeType.startsWith("audio/") ?? false);
    const isStrip = computed(() => playerMode.value === "strip");

    const setPlayerMode = (mode: "expanded" | "pip" | "strip") => {
      playerMode.value = mode;
    };
    const clearActiveFile = () => {
      activeFile.value = null;
    };
    const setSnapCorner = (c: "tl" | "tr" | "bl" | "br") => {
      snapCorner.value = c;
    };
    const setVolume = (v: number) => {
      volume.value = Math.max(0, Math.min(1, v));
    };

    const shuffle = () => {
      if (queue.value.length < 2) return;
      const items = [...queue.value];
      for (let i = items.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [items[i], items[j]] = [items[j], items[i]];
      }
      queue.value = items;
      shuffled.value = true;
      if (activeFile.value) {
        currentIndex.value = items.findIndex((f) => f.fileId === activeFile.value!.fileId);
      }
    };

    const updateQueue = (files: MediaFileDto[]) => {
      queue.value = files;
      // Re-anchor currentIndex in case the list changed, but don't touch activeFile
      if (activeFile.value) {
        const idx = files.findIndex((f) => f.fileId === activeFile.value!.fileId);
        currentIndex.value = idx; // -1 if the active file disappeared from the list
      }
    };

    const setQueue = (files: MediaFileDto[], startIndex = 0, playlistId?: string) => {
      queue.value = files;
      currentIndex.value = startIndex;
      activeFile.value = files[startIndex] ?? null;
      activePlaylistId.value = playlistId ?? null;
      shuffled.value = false;
    };

    const setCurrentIndex = (idx: number) => {
      currentIndex.value = idx;
      activeFile.value = queue.value[idx] ?? null;
    };

    const setActiveFile = (file: MediaFileDto) => {
      activeFile.value = file;
      const idx = queue.value.findIndex((f) => f.fileId === file.fileId);
      currentIndex.value = idx;
    };

    const toggleLoop = () => {
      loop.value = !loop.value;
    };

    const next = () => {
      if (hasNext.value) {
        currentIndex.value++;
        activeFile.value = queue.value[currentIndex.value];
      } else if (loop.value) {
        currentIndex.value = 0;
        activeFile.value = queue.value[0];
      }
    };

    const previous = () => {
      if (!hasPrevious.value) return;
      currentIndex.value--;
      activeFile.value = queue.value[currentIndex.value];
    };

    const toggleAutoplay = () => {
      autoplay.value = !autoplay.value;
    };

    return {
      activeFile,
      hasActiveFile,
      currentIndex,
      queue,
      playerMode,
      autoplay,
      isStrip,
      isAudio,
      loop,
      shuffled,
      snapCorner,
      hasNext,
      updateQueue,
      hasPrevious,
      activePlaylistId,
      setQueue,
      setCurrentIndex,
      setActiveFile,
      setPlayerMode,
      previous,
      next,
      toggleLoop,
      shuffle,
      toggleAutoplay,
      volume,
      clearActiveFile,
      setSnapCorner,
      setVolume,
    };
  },
  {
    persist: true,
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
