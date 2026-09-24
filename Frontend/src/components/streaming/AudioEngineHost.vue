<template>
  <video ref="videoRef" class="audio-engine-media" playsinline preload="auto" />
</template>

<script setup lang="ts">
import { ref } from "vue";

import { usePlayerEngine } from "@/composables/usePlayerEngine";

const videoRef = ref<HTMLVideoElement | null>(null);
const containerRef = ref<HTMLElement | null>(null);

// Single dashboard-owned audio engine. Presentation surfaces (strip, pill,
// card, sheet) consume the player store instead of owning media elements, so
// route and responsive changes never interrupt playback.
usePlayerEngine(videoRef, containerRef, { headless: true, mediaKind: "audio" });
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
