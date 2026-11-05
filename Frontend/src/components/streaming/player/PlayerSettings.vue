<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { onBeforeUnmount, onMounted, ref, watch } from "vue";

import { usePlayerStore } from "@/stores/stream-player";

import SettingsContent from "./SettingsContent.vue";

const store = usePlayerStore();

const isOpen = ref(false);
const btnRef = ref<HTMLButtonElement | null>(null);
const panelStyle = ref<Record<string, string>>({});

const PANEL_W = 256;
const PANEL_MARGIN = 16;

const reposition = () => {
  if (!btnRef.value) return;
  const r = btnRef.value.getBoundingClientRect();

  // Align panel's right edge with the button's right edge, then clamp
  // so it never bleeds off the left or right side of the viewport.
  const left = Math.min(
    Math.max(r.right - PANEL_W, PANEL_MARGIN),
    window.innerWidth - PANEL_W - PANEL_MARGIN,
  );

  // Anchor above the strip row itself so the panel never sits on the strip
  // edge. Falls back to the trigger button when the strip is not mounted.
  const stripTop = document.getElementById("audio-strip")?.getBoundingClientRect().top ?? r.top;
  const bottom = window.innerHeight - stripTop + PANEL_MARGIN;

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
  if (isOpen.value) reposition();
};

const close = () => {
  isOpen.value = false;
};

const onResize = () => {
  if (isOpen.value) reposition();
};

const onDocClick = (e: MouseEvent) => {
  if (!isOpen.value) return;
  const panel = document.getElementById("player-settings-panel");
  if (panel?.contains(e.target as Node) || btnRef.value?.contains(e.target as Node)) return;
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
</script>

<template>
  <div class="relative">
    <button
      ref="btnRef"
      class="w-7 h-7 rounded-lg flex items-center justify-center transition-colors"
      :class="
        isOpen
          ? 'text-primary'
          : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
      "
      title="Player settings"
      @click.stop="toggle"
    >
      <Icon icon="mdi:tune-variant" class="w-4 h-4" />
    </button>

    <Teleport to="body">
      <Transition name="settings-panel">
        <div
          v-if="isOpen"
          id="player-settings-panel"
          class="player-card frosted-glass glass-surface-strong border border-black/[0.08] dark:border-white/10 rounded-2xl shadow-2xl overflow-hidden flex flex-col"
          :style="panelStyle"
        >
          <SettingsContent />
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.settings-panel-enter-active {
  transition:
    opacity 200ms ease-out,
    transform 200ms ease-out;
}
.settings-panel-leave-active {
  transition:
    opacity 150ms ease-in,
    transform 150ms ease-in;
}
.settings-panel-enter-from,
.settings-panel-leave-to {
  opacity: 0;
  transform: translateY(8px) scale(0.98);
}

@media (prefers-reduced-motion: reduce) {
  .settings-panel-enter-active,
  .settings-panel-leave-active {
    transition: none;
  }
  .settings-panel-enter-from,
  .settings-panel-leave-to {
    transform: none;
  }
}
</style>
