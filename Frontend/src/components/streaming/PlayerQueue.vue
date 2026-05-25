<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const { queue, currentIndex, activeFile, loop, shuffled } = storeToRefs(store);

const isOpen = ref(false);
const listRef = ref<HTMLElement | null>(null);
const btnRef = ref<HTMLButtonElement | null>(null);

// Fixed position of the panel, recalculated whenever it opens or the window resizes
const panelStyle = ref<Record<string, string>>({});

const PANEL_W = 340;
const PANEL_MARGIN = 8; // gap between strip top edge and panel bottom

const reposition = () => {
  if (!btnRef.value) return;
  const r = btnRef.value.getBoundingClientRect();
  // Anchor bottom of panel to top of strip row, right-aligned to button
  const bottom = window.innerHeight - r.top + PANEL_MARGIN;
  const right = window.innerWidth - r.right;
  panelStyle.value = {
    position: "fixed",
    bottom: `${bottom}px`,
    right: `${right}px`,
    width: `${PANEL_W}px`,
    maxHeight: `min(60vh, 480px)`,
    zIndex: "10000",
  };
};

const toggle = () => {
  isOpen.value = !isOpen.value;
  if (isOpen.value) {
    reposition();
    nextTick(() => scrollToActive(false));
  }
};

const close = () => {
  isOpen.value = false;
};

watch(currentIndex, () => {
  if (isOpen.value) nextTick(() => scrollToActive(true));
});

const scrollToActive = (smooth = true) => {
  if (!listRef.value) return;
  const active = listRef.value.querySelector<HTMLElement>("[data-active='true']");
  active?.scrollIntoView({ block: "nearest", behavior: smooth ? "smooth" : "instant" });
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

const fmtDuration = (secs: number): string => {
  if (!secs || !isFinite(secs)) return "--:--";
  const m = Math.floor(secs / 60);
  const s = Math.floor(secs % 60);
  return `${m}:${String(s).padStart(2, "0")}`;
};

const trackLabel = (index: number): string => {
  const item = queue.value[index];
  return item?.title || item?.fileName || "Unknown track";
};

const artistLabel = (index: number): string => {
  const item = queue.value[index];
  return item?.artist || item?.album || "";
};

defineExpose({ toggle, close, isOpen });
</script>

<template>
  <div class="relative">
    <!-- Toggle button -->
    <button
      ref="btnRef"
      class="queue-toggle w-7 h-7 rounded-lg flex items-center justify-center transition-colors"
      :class="
        isOpen
          ? 'text-primary dark:text-primary'
          : 'text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80'
      "
      :title="isOpen ? 'Close queue' : 'Show queue'"
      @click.stop="toggle"
    >
      <Icon icon="mdi:playlist-play" class="w-4 h-4" />
    </button>

    <!--
      Teleport to body so no ancestor overflow:hidden / z-index context can
      clip or bury the panel. Position is fixed, recalculated from the button's
      getBoundingClientRect on every open and window resize.
    -->
    <Teleport to="body">
      <Transition name="queue-panel">
        <div
          v-if="isOpen"
          id="player-queue-panel"
          class="bg-white/80 dark:bg-[#0e0e12]/90 backdrop-blur-2xl border border-black/[0.08] dark:border-white/[0.09] rounded-2xl overflow-hidden shadow-2xl flex flex-col"
          :style="panelStyle"
        >
          <!-- Header -->
          <div
            class="flex items-center justify-between px-4 py-3 border-b border-black/[0.06] dark:border-white/[0.07] flex-shrink-0"
          >
            <div class="flex items-center gap-2">
              <span
                class="text-[11px] font-semibold uppercase tracking-widest text-gray-400 dark:text-white/35 select-none"
              >
                Queue
              </span>
              <span
                v-if="queue.length"
                class="text-[10px] font-semibold tabular-nums px-1.5 py-0.5 rounded-full bg-black/[0.06] dark:bg-white/[0.08] text-gray-500 dark:text-white/40"
              >
                {{ queue.length }}
              </span>
            </div>

            <div class="flex items-center gap-0.5">
              <span
                v-if="shuffled"
                class="flex items-center gap-1 text-[10px] font-medium text-primary dark:text-primary bg-primary/10 dark:bg-primary/[0.12] px-1.5 py-0.5 rounded-full select-none"
              >
                <Icon icon="mdi:shuffle-variant" class="w-3 h-3" />
                shuffled
              </span>
              <span
                v-if="loop"
                class="flex items-center gap-1 text-[10px] font-medium text-primary dark:text-primary bg-primary/10 dark:bg-primary/[0.12] px-1.5 py-0.5 rounded-full ml-1 select-none"
              >
                <Icon icon="mdi:repeat" class="w-3 h-3" />
                loop
              </span>
              <button
                class="ml-1.5 w-6 h-6 rounded-lg flex items-center justify-center text-gray-400 dark:text-white/30 hover:text-gray-700 dark:hover:text-white/70 transition-colors"
                @click.stop="close"
              >
                <Icon icon="mdi:close" class="w-3.5 h-3.5" />
              </button>
            </div>
          </div>

          <!-- Track list -->
          <ol
            ref="listRef"
            class="overflow-y-auto overscroll-contain flex-1 py-1.5"
            role="listbox"
            aria-label="Playback queue"
          >
            <li
              v-for="(item, idx) in queue"
              :key="item.fileId"
              :data-active="idx === currentIndex"
              role="option"
              :aria-selected="idx === currentIndex"
              class="queue-row group relative flex items-center gap-3 px-3 py-2 mx-1.5 rounded-xl cursor-pointer select-none transition-colors"
              :class="
                idx === currentIndex
                  ? 'bg-primary/10 dark:bg-primary/20'
                  : 'hover:bg-black/[0.04] dark:hover:bg-white/[0.04]'
              "
              @click="store.setCurrentIndex(idx)"
            >
              <!-- Left: index / playing indicator -->
              <div class="w-6 flex-shrink-0 flex items-center justify-center">
                <template v-if="idx === currentIndex">
                  <span class="wave-bars" aria-hidden="true">
                    <span class="bar" />
                    <span class="bar" />
                    <span class="bar" />
                  </span>
                </template>
                <template v-else>
                  <span
                    class="text-[11px] tabular-nums text-gray-300 dark:text-white/20 group-hover:hidden"
                  >
                    {{ idx + 1 }}
                  </span>
                  <Icon
                    icon="mdi:play"
                    class="w-3.5 h-3.5 text-gray-400 dark:text-white/40 hidden group-hover:block"
                  />
                </template>
              </div>

              <!-- Track info -->
              <div class="flex-1 min-w-0">
                <p
                  class="text-[13px] font-medium truncate leading-tight"
                  :class="
                    idx === currentIndex
                      ? 'text-primary dark:text-primary'
                      : 'text-gray-800 dark:text-white/80'
                  "
                >
                  {{ trackLabel(idx) }}
                </p>
                <p
                  v-if="artistLabel(idx)"
                  class="text-[11px] truncate mt-0.5 text-gray-400 dark:text-white/30"
                >
                  {{ artistLabel(idx) }}
                </p>
              </div>

              <Icon
                v-if="item.isVideo"
                icon="mdi:video-outline"
                class="w-3.5 h-3.5 flex-shrink-0 text-gray-300 dark:text-white/20"
              />

              <span class="text-[11px] tabular-nums flex-shrink-0 text-gray-400 dark:text-white/30">
                {{ fmtDuration(item.duration) }}
              </span>

              <span
                v-if="idx === currentIndex"
                class="absolute left-0 top-2 bottom-2 w-[3px] rounded-full bg-primary"
                aria-hidden="true"
              />
            </li>

            <li
              v-if="!queue.length"
              class="flex flex-col items-center justify-center gap-2 py-10 text-gray-300 dark:text-white/20"
            >
              <Icon icon="mdi:playlist-remove" class="w-8 h-8" />
              <span class="text-xs">Queue is empty</span>
            </li>
          </ol>

          <!-- Footer -->
          <div
            v-if="activeFile"
            class="flex items-center gap-2 px-4 py-2.5 border-t border-black/[0.06] dark:border-white/[0.07] flex-shrink-0"
          >
            <Icon icon="mdi:music-note" class="w-3.5 h-3.5 flex-shrink-0 text-primary" />
            <p class="text-[11px] text-gray-400 dark:text-white/30 truncate">
              Playing
              <span class="text-gray-600 dark:text-white/60 font-medium">
                {{ activeFile.title || activeFile.fileName }}
              </span>
              <template v-if="queue.length > 1">
                · {{ currentIndex + 1 }} of {{ queue.length }}
              </template>
            </p>
          </div>
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

.wave-bars {
  display: flex;
  align-items: flex-end;
  gap: 1.5px;
  height: 14px;
}

.wave-bars .bar {
  display: block;
  width: 2.5px;
  border-radius: 2px;
  background: var(--color-primary);
  animation: wave-bounce 0.9s ease-in-out infinite;
}

.wave-bars .bar:nth-child(1) {
  animation-delay: 0s;
  animation-duration: 0.75s;
}
.wave-bars .bar:nth-child(2) {
  animation-delay: 0.18s;
  animation-duration: 0.9s;
}
.wave-bars .bar:nth-child(3) {
  animation-delay: 0.36s;
  animation-duration: 0.8s;
}

@keyframes wave-bounce {
  0%,
  100% {
    height: 4px;
  }
  50% {
    height: 13px;
  }
}

ol::-webkit-scrollbar {
  width: 4px;
}
ol::-webkit-scrollbar-track {
  background: transparent;
}
ol::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.12);
  border-radius: 4px;
}
</style>
