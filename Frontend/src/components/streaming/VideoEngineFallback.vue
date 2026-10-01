<template>
  <video ref="videoRef" class="video-engine-fallback" playsinline preload="auto" />
</template>

<script setup lang="ts">
import { ref } from "vue";

import { usePlayerEngine } from "@/composables/usePlayerEngine";

const videoRef = ref<HTMLVideoElement | null>(null);
const containerRef = ref<HTMLElement | null>(null);

// Headless video engine, mounted by AudioEngineHost only while a video is
// current and no visible video surface exists. It does not count as a surface,
// so it unmounts as soon as one mounts and takes over the same track.
usePlayerEngine(videoRef, containerRef, { headless: true, mediaKind: "video" });
</script>

<style scoped>
.video-engine-fallback {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}
</style>
