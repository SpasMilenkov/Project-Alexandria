<template>
  <video ref="videoRef" class="audio-engine-media" playsinline preload="auto" />
  <VideoEngineFallback v-if="needsVideoFallback" />
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

import { usePlayerEngine } from "@/composables/usePlayerEngine";
import { usePlayerStore } from "@/stores/stream-player";

import VideoEngineFallback from "./VideoEngineFallback.vue";

const store = usePlayerStore();
const videoRef = ref<HTMLVideoElement | null>(null);
const containerRef = ref<HTMLElement | null>(null);

// Single dashboard-owned audio engine. Presentation surfaces (strip, pill,
// card, sheet) consume the player store instead of owning media elements, so
// route and responsive changes never interrupt playback.
usePlayerEngine(videoRef, containerRef, { headless: true, mediaKind: "audio" });

// A video can become current (queued after audio, resumed after navigation)
// while no video surface is mounted. Without an engine it would sit selected
// and silent, so a headless one takes over until a real surface mounts.
const needsVideoFallback = computed(
  () => store.nowPlaying !== null && !store.isAudio && store.videoSurfaces === 0,
);
</script>

<style scoped>
.audio-engine-media {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}
</style>
