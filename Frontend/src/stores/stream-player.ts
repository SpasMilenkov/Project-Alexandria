// oxlint-disable max-statements
// oxlint-disable max-lines-per-function
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

export interface VariantTrack {
  id: number;
  height: number | null;
  width: number | null;
  bandwidth: number;
  videoCodec: string | null;
  audioCodec: string | null;
  active: boolean;
}

type EngineControls = {
  play: () => void;
  pause: () => void;
  seek: (seconds: number) => void;
  selectVariant: (id: number | null) => void;
  setPlaybackRate: (rate: number) => void;
};

let _engine: EngineControls | null = null;

export const usePlayerStore = defineStore(
  "player",
  () => {
    // Persisted state
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

    // Runtime-only playback state
    const currentTime = ref(0);
    const duration = ref(0);
    const isPlaying = ref(false);

    // Runtime-only quality state
    const variantTracks = ref<VariantTrack[]>([]);
    const activeVariantId = ref<number | null>(null); // null = ABR is choosing
    const abrEnabled = ref(true);
    const playbackRate = ref(1);

    // Computed
    const hasNext = computed(() => currentIndex.value < queue.value.length - 1);
    const hasPrevious = computed(() => currentIndex.value > 0);
    const hasActiveFile = computed(() => activeFile.value !== null);
    const isAudio = computed(() => activeFile.value?.mimeType.startsWith("audio/") ?? false);
    const isStrip = computed(() => playerMode.value === "strip");

    // Engine bridge
    const registerEngine = (controls: EngineControls) => {
      _engine = controls;
    };
    const unregisterEngine = () => {
      _engine = null;
    };

    // Playback controls — called by presentational components
    const play = () => _engine?.play();
    const pause = () => _engine?.pause();
    const seek = (seconds: number) => _engine?.seek(seconds);
    const togglePlay = () => {
      if (!_engine) return;
      if (isPlaying.value) {
        _engine.pause();
      } else {
        _engine.play();
      }
    };

    // Quality controls
    const selectVariant = (id: number | null) => _engine?.selectVariant(id);
    const setPlaybackRate = (rate: number) => _engine?.setPlaybackRate(rate);

    // Setters for runtime state — called only by usePlayerEngine
    const setCurrentTime = (t: number) => {
      currentTime.value = t;
    };
    const setDuration = (d: number) => {
      duration.value = d;
    };
    const setIsPlaying = (v: boolean) => {
      isPlaying.value = v;
    };
    const setVariantTracks = (tracks: VariantTrack[]) => {
      variantTracks.value = tracks;
    };
    const setActiveVariantId = (id: number | null) => {
      activeVariantId.value = id;
    };
    const setAbrEnabled = (v: boolean) => {
      abrEnabled.value = v;
    };
    const setPlaybackRateState = (rate: number) => {
      playbackRate.value = rate;
    };

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
      if (activeFile.value) {
        const idx = files.findIndex((f) => f.fileId === activeFile.value!.fileId);
        currentIndex.value = idx;
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
      // Persisted
      activeFile,
      snapCorner,
      volume,
      queue,
      currentIndex,
      autoplay,
      playerMode,
      activePlaylistId,
      loop,
      shuffled,
      // Runtime
      currentTime,
      duration,
      isPlaying,
      variantTracks,
      activeVariantId,
      abrEnabled,
      playbackRate,
      // Computed
      hasNext,
      hasPrevious,
      hasActiveFile,
      isAudio,
      isStrip,
      // Engine bridge
      registerEngine,
      unregisterEngine,
      // Playback actions
      play,
      pause,
      seek,
      togglePlay,
      // Quality actions
      selectVariant,
      setPlaybackRate,
      // Engine-only setters
      setCurrentTime,
      setDuration,
      setIsPlaying,
      setVariantTracks,
      setActiveVariantId,
      setAbrEnabled,
      setPlaybackRateState,
      // Standard actions
      setPlayerMode,
      clearActiveFile,
      setSnapCorner,
      setVolume,
      shuffle,
      updateQueue,
      setQueue,
      setCurrentIndex,
      setActiveFile,
      toggleLoop,
      next,
      previous,
      toggleAutoplay,
    };
  },
  {
    persist: {
      pick: [
        "activeFile",
        "snapCorner",
        "volume",
        "queue",
        "currentIndex",
        "autoplay",
        "playerMode",
        "activePlaylistId",
        "loop",
        "shuffled",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
