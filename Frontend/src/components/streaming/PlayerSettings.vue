<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, onBeforeUnmount, onMounted, ref } from "vue";

import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const { variantTracks, activeVariantId, abrEnabled, playbackRate, activeFile } = storeToRefs(store);

const isOpen = ref(false);
const btnRef = ref<HTMLButtonElement | null>(null);
const panelStyle = ref<Record<string, string>>({});

const PANEL_W = 256;
const PANEL_MARGIN = 8;

const reposition = () => {
  if (!btnRef.value) return;
  const r = btnRef.value.getBoundingClientRect();

  // Align panel's right edge with the button's right edge, then clamp
  // so it never bleeds off the left or right side of the viewport.
  const left = Math.min(
    Math.max(r.right - PANEL_W, PANEL_MARGIN),
    window.innerWidth - PANEL_W - PANEL_MARGIN,
  );

  panelStyle.value = {
    position: "fixed",
    bottom: `${window.innerHeight - r.top + PANEL_MARGIN}px`,
    left: `${left}px`,
    width: `${PANEL_W}px`,
    zIndex: "10000",
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

// Quality options

const isAudioOnly = computed(() => activeFile.value?.mimeType.startsWith("audio/") ?? false);

const qualityOptions = computed(() => {
  if (!variantTracks.value.length) return [];

  if (isAudioOnly.value) {
    const seen = new Set<number>();
    return variantTracks.value
      .slice()
      .sort((a, b) => b.bandwidth - a.bandwidth)
      .filter((t) => {
        const bucket = Math.round(t.bandwidth / 32_000) * 32;
        if (seen.has(bucket)) return false;
        seen.add(bucket);
        return true;
      });
  }

  const byHeight = new Map<number, (typeof variantTracks.value)[0]>();
  for (const t of variantTracks.value) {
    const h = t.height ?? 0;
    const existing = byHeight.get(h);
    if (!existing || t.bandwidth > existing.bandwidth) byHeight.set(h, t);
  }
  return Array.from(byHeight.values()).sort((a, b) => (b.height ?? 0) - (a.height ?? 0));
});

const activeTrack = computed(() => variantTracks.value.find((t) => t.active) ?? null);

const autoLabel = computed(() => {
  if (!activeTrack.value) return "Auto";
  return isAudioOnly.value
    ? `Auto · ${fmtBitrate(activeTrack.value.bandwidth)}`
    : `Auto · ${activeTrack.value.height}p`;
});

const fmtBitrate = (bps: number) => {
  if (bps >= 1_000_000) return `${(bps / 1_000_000).toFixed(1)} Mbps`;
  return `${Math.round(bps / 1_000)} kbps`;
};

const trackLabel = (t: (typeof variantTracks.value)[0]) =>
  isAudioOnly.value ? fmtBitrate(t.bandwidth) : `${t.height}p`;

const trackSublabel = (t: (typeof variantTracks.value)[0]) =>
  isAudioOnly.value ? (t.audioCodec ?? "") : fmtBitrate(t.bandwidth);

const SPEED_OPTIONS = [0.5, 0.75, 1, 1.25, 1.5, 2];
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
          class="bg-white/75 dark:bg-white/[0.06] backdrop-blur-xl border border-gray-200/70 dark:border-gray-700/70 rounded-2xl shadow-2xl overflow-hidden flex flex-col"
          :style="panelStyle"
        >
          <!-- Quality -->
          <div class="p-4 pb-3">
            <p
              class="text-[10px] font-semibold uppercase tracking-widest text-dimmed select-none mb-3"
            >
              Quality
            </p>

            <p v-if="!qualityOptions.length" class="text-xs text-gray-400 dark:text-white/30 py-1">
              Loading tracks…
            </p>

            <div v-else class="flex flex-col gap-0.5">
              <!-- Auto -->
              <button
                class="flex items-center gap-3 w-full px-2 py-2 rounded-xl text-left transition-all duration-200 ease-out"
                :class="
                  abrEnabled ? 'bg-primary/10' : 'hover:bg-gray-100/60 dark:hover:bg-white/[0.05]'
                "
                @click="store.selectVariant(null)"
              >
                <span
                  class="w-3.5 h-3.5 rounded-full border-2 flex-shrink-0 transition-all duration-200 ease-out"
                  :class="
                    abrEnabled
                      ? 'border-primary bg-primary'
                      : 'border-gray-300 dark:border-white/20'
                  "
                />
                <span class="flex-1 min-w-0 flex items-center justify-between gap-2">
                  <span
                    class="text-[13px] font-medium truncate"
                    :class="abrEnabled ? 'text-primary' : 'text-gray-800 dark:text-white/80'"
                  >
                    {{ autoLabel }}
                  </span>
                  <Icon
                    v-if="abrEnabled"
                    icon="mdi:lightning-bolt"
                    class="w-3.5 h-3.5 flex-shrink-0 text-primary"
                  />
                </span>
              </button>

              <!-- Specific tracks -->
              <button
                v-for="track in qualityOptions"
                :key="track.id"
                class="flex items-center gap-3 w-full px-2 py-2 rounded-xl text-left transition-all duration-200 ease-out"
                :class="
                  !abrEnabled && activeVariantId === track.id
                    ? 'bg-primary/10'
                    : 'hover:bg-gray-100/60 dark:hover:bg-white/[0.05]'
                "
                @click="store.selectVariant(track.id)"
              >
                <span
                  class="w-3.5 h-3.5 rounded-full border-2 flex-shrink-0 transition-all duration-200 ease-out"
                  :class="
                    !abrEnabled && activeVariantId === track.id
                      ? 'border-primary bg-primary'
                      : 'border-gray-300 dark:border-white/20'
                  "
                />
                <span class="flex-1 min-w-0">
                  <span
                    class="text-[13px] font-medium"
                    :class="
                      !abrEnabled && activeVariantId === track.id
                        ? 'text-primary'
                        : 'text-gray-800 dark:text-white/80'
                    "
                  >
                    {{ trackLabel(track) }}
                  </span>
                  <span class="text-[11px] text-gray-400 dark:text-white/30 ml-2">
                    {{ trackSublabel(track) }}
                  </span>
                </span>
              </button>
            </div>
          </div>

          <div class="mx-4 border-t border-gray-200/70 dark:border-gray-700/70" />

          <!-- Speed -->
          <div class="p-4 pt-3">
            <p
              class="text-[10px] font-semibold uppercase tracking-widest text-dimmed select-none mb-3"
            >
              Speed
            </p>
            <div class="flex gap-1">
              <button
                v-for="speed in SPEED_OPTIONS"
                :key="speed"
                class="flex-1 py-1.5 rounded-lg text-[12px] font-medium transition-all duration-200 ease-out"
                :class="
                  playbackRate === speed
                    ? 'bg-primary text-white'
                    : 'bg-gray-100/60 dark:bg-white/[0.06] text-gray-600 dark:text-white/60 hover:bg-gray-200/60 dark:hover:bg-white/[0.09]'
                "
                @click="store.setPlaybackRate(speed)"
              >
                {{ speed === 1 ? "1×" : `${speed}×` }}
              </button>
            </div>
          </div>
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
</style>
