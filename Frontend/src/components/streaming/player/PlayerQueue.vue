<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

import { usePlayerStore } from "@/stores/stream-player";

import QueueContent from "./QueueContent.vue";

const store = usePlayerStore();

const isOpen = ref(false);
const btnRef = ref<HTMLButtonElement | null>(null);
const contentRef = ref<InstanceType<typeof QueueContent> | null>(null);

// Fixed position of the panel, recalculated whenever it opens or the window resizes
const panelStyle = ref<Record<string, string>>({});

const PANEL_W = 340;
const PANEL_MARGIN = 16; // gap between strip top edge and panel bottom

const reposition = () => {
  if (!btnRef.value) return;
  const r = btnRef.value.getBoundingClientRect();

  // Anchor above the strip row itself so the panel never sits on the strip
  // edge. Falls back to the trigger button when the strip is not mounted.
  const stripTop = document.getElementById("audio-strip")?.getBoundingClientRect().top ?? r.top;
  const bottom = window.innerHeight - stripTop + PANEL_MARGIN;
  const left = Math.min(
    Math.max(r.right - PANEL_W, PANEL_MARGIN),
    window.innerWidth - PANEL_W - PANEL_MARGIN,
  );

  panelStyle.value = {
    position: "fixed",
    bottom: `${bottom}px`,
    left: `${left}px`,
    width: `${PANEL_W}px`,
    zIndex: "40",
  };
};

const toggle = () => {
  isOpen.value = !isOpen.value;
  if (isOpen.value) {
    reposition();
    void nextTick().then(() => contentRef.value?.scrollToActive(false));
  }
};

const close = () => {
  isOpen.value = false;
};

const onResize = () => {
  if (isOpen.value) reposition();
};

const onDocClick = (e: MouseEvent) => {
  if (!isOpen.value) return;
  const target = e.target as Node;
  const panel = document.getElementById("player-queue-panel");
  if (panel?.contains(target) || btnRef.value?.contains(target)) return;
  close();
};

onMounted(() => {
  window.addEventListener("resize", onResize);
  document.addEventListener("click", onDocClick, { capture: true });
});

onBeforeUnmount(() => {
  window.removeEventListener("resize", onResize);
  document.removeEventListener("click", onDocClick, { capture: true });
});

// Metadata navigation closes the popover without touching playback or queue.
watch(
  () => store.transientEpoch,
  () => close(),
);

defineExpose({ toggle, close, isOpen });
</script>

<template>
  <div class="relative">
    <!-- Toggle button -->
    <button
      ref="btnRef"
      class="queue-toggle w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
      :class="
        isOpen
          ? 'text-primary dark:text-primary'
          : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
      "
      :title="isOpen ? 'Close queue' : 'Show queue'"
      @click.stop="toggle"
    >
      <Icon icon="mdi:playlist-play" class="w-5 h-5" />
    </button>

    <!--
      Teleport to body so no ancestor overflow:hidden / z-index context can
      clip or bury the panel. Position is fixed, recalculated from the button's
      getBoundingClientRect on every open and window resize.
    -->
    <Teleport to="body">
      <Transition name="queue-panel">
        <div v-if="isOpen" id="player-queue-panel" :style="panelStyle">
          <QueueContent @close="close" />
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.queue-panel-enter-active {
  transition:
    opacity 200ms ease,
    transform 220ms cubic-bezier(0.34, 1.4, 0.64, 1);
}
.queue-panel-leave-active {
  transition:
    opacity 160ms ease,
    transform 160ms ease;
}
.queue-panel-enter-from,
.queue-panel-leave-to {
  opacity: 0;
  transform: translateY(8px) scale(0.98);
}

@media (prefers-reduced-motion: reduce) {
  .queue-panel-enter-active,
  .queue-panel-leave-active {
    transition: none;
  }
  .queue-panel-enter-from,
  .queue-panel-leave-to {
    transform: none;
  }
}
</style>
