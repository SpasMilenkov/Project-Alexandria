<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, onMounted, ref } from "vue";

import PlayerQueue from "@/components/streaming/PlayerQueue.vue";
import { usePlayerEngine } from "@/composables/usePlayerEngine";
import { getPreview } from "@/queries/files";
import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const { activeFile, hasNext, hasPrevious, loop, shuffled } = storeToRefs(store);

const containerRef = ref<HTMLDivElement | null>(null);
const videoRef = ref<HTMLVideoElement | null>(null);

const { data: preview } = useQuery(() => getPreview(activeFile.value?.fileId ?? ""));
const thumbnailUrl = computed(() => preview.value?.thumbnailUrl ?? null);

const activeTitle = computed(
  () => activeFile.value?.title || activeFile.value?.fileName || "Unknown",
);
const activeArtist = computed(() => activeFile.value?.artist || activeFile.value?.album || null);

// Video skin always uses expanded mode with fullscreen support.
onMounted(() => store.setPlayerMode("expanded"));

const { isBuffering, loadError } = usePlayerEngine(videoRef, containerRef, {
  getThumbnailUrl: () => thumbnailUrl.value,
  shakaUiConfig: {
    controlPanelElements: [
      "play_pause",
      "time_and_duration",
      "spacer",
      "mute",
      "volume",
      "quality",
      "fullscreen",
    ],
    addSeekBar: true,
    fadeDelay: 3,
    enableTooltips: true,
    seekBarColors: {
      base: "rgba(255,255,255,0.18)",
      buffered: "rgba(255,255,255,0.35)",
      played: "rgb(99,102,241)",
    },
  },
});
</script>

<template>
  <!--
    Video skin takes over the full content area of the layout panel.
    The layout's slot already fills flex-1 min-h-0 h-full, so this div
    gets the remaining height after the sidebar and navbar.
  -->
  <div class="flex flex-col h-full bg-black/95 dark:bg-black">
    <!-- Top bar -->
    <div class="flex items-center gap-3 px-4 py-3 flex-shrink-0 border-b border-white/[0.07]">
      <!-- Thumbnail -->
      <div class="w-8 h-8 rounded-md overflow-hidden bg-white/[0.06] flex-shrink-0">
        <img
          v-if="thumbnailUrl"
          :src="thumbnailUrl"
          :alt="activeTitle"
          class="w-full h-full object-cover"
        />
        <div v-else class="w-full h-full flex items-center justify-center">
          <Icon icon="mdi:video-outline" class="w-4 h-4 text-white/30" />
        </div>
      </div>

      <!-- Title + artist -->
      <div class="flex-1 min-w-0">
        <p class="text-sm font-semibold text-white/90 truncate leading-tight">
          {{ activeTitle }}
        </p>
        <p v-if="activeArtist" class="text-xs text-white/40 truncate mt-0.5">
          {{ activeArtist }}
        </p>
      </div>

      <!-- Controls cluster -->
      <div class="flex items-center gap-1 flex-shrink-0">
        <button
          class="w-8 h-8 rounded-lg flex items-center justify-center text-white/40 hover:text-white/80 disabled:opacity-25 disabled:cursor-not-allowed transition-colors"
          :disabled="!hasPrevious"
          @click="store.previous()"
        >
          <Icon icon="mdi:skip-previous" class="w-5 h-5" />
        </button>

        <button
          class="w-8 h-8 rounded-lg flex items-center justify-center text-white/40 hover:text-white/80 disabled:opacity-25 disabled:cursor-not-allowed transition-colors"
          :disabled="!hasNext"
          @click="store.next()"
        >
          <Icon icon="mdi:skip-next" class="w-5 h-5" />
        </button>

        <div class="w-px h-4 bg-white/10 mx-1" />

        <button
          class="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
          :class="shuffled ? 'text-indigo-400' : 'text-white/40 hover:text-white/80'"
          @click="store.shuffle()"
        >
          <Icon icon="mdi:shuffle-variant" class="w-4 h-4" />
        </button>

        <button
          class="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
          :class="loop ? 'text-indigo-400' : 'text-white/40 hover:text-white/80'"
          @click="store.toggleLoop()"
        >
          <Icon icon="mdi:repeat" class="w-4 h-4" />
        </button>

        <PlayerQueue />
      </div>
    </div>

    <!-- Video stage -->
    <div class="flex-1 min-h-0 flex items-center justify-center p-4 md:p-8">
      <!-- Error state -->
      <Transition name="fade">
        <div
          v-if="loadError"
          class="absolute inset-0 flex flex-col items-center justify-center gap-3 z-10"
        >
          <Icon icon="mdi:alert-circle-outline" class="w-10 h-10 text-red-400/70" />
          <p class="text-sm text-white/50 max-w-xs text-center">{{ loadError }}</p>
        </div>
      </Transition>

      <!-- Buffering overlay -->
      <Transition name="fade">
        <div
          v-if="isBuffering && !loadError"
          class="absolute inset-0 flex items-center justify-center z-10 pointer-events-none"
        >
          <div
            class="w-10 h-10 rounded-full border-2 border-white/20 border-t-indigo-400 animate-spin"
          />
        </div>
      </Transition>

      <!--
        Shaka container. Same rules as AudioPlayerSkin: containerRef and videoRef
        never move after the engine attaches. The aspect-video wrapper just constrains
        the visual size while Shaka's overlay fills the container absolutely.
      -->
      <div
        ref="containerRef"
        class="relative w-full max-w-5xl rounded-xl overflow-hidden shadow-[0_0_80px_rgba(0,0,0,0.8)]"
        style="aspect-ratio: 16 / 9"
        data-shaka-player-container
      >
        <video
          ref="videoRef"
          class="absolute inset-0 w-full h-full block bg-black"
          data-shaka-player
          playsinline
        />
      </div>
    </div>
  </div>
</template>

<style>
@import "shaka-player/dist/controls.css";

/* Scope all overrides to this skin's container to avoid leaking into AudioPlayerSkin. */

.video-skin-container .shaka-video-container {
  background: transparent !important;
}

.video-skin-container .shaka-bottom-controls {
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.65) 0%,
    rgba(0, 0, 0, 0.2) 60%,
    transparent 100%
  );
  padding: 0 20px 16px;
  border-radius: 0 0 0.75rem 0.75rem;
}

.video-skin-container .shaka-scrim-container {
  background: transparent !important;
}

.video-skin-container .shaka-play-button {
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.14);
  border: 1px solid rgba(255, 255, 255, 0.18);
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(10px);
  transition:
    background 160ms ease,
    transform 160ms ease;
}

.video-skin-container .shaka-play-button:hover {
  background: rgba(var(--ui-primary), 0.7);
  transform: scale(1.04);
}

.video-skin-container .shaka-controls-button-panel {
  align-items: center;
  gap: 4px;
}

.video-skin-container .shaka-asset-title,
.video-skin-container .shaka-content-title {
  display: none !important;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 200ms ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
