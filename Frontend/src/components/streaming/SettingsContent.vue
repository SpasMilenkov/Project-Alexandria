<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed } from "vue";

import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const { variantTracks, activeVariantId, abrEnabled, playbackRate, activeFile } = storeToRefs(store);

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
  <div class="flex h-full min-h-0 max-h-[min(60dvh,400px)] flex-col overflow-y-auto">
    <!-- Quality -->
    <div class="p-4 pb-3">
      <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed select-none mb-3">
        Quality
      </p>

      <p v-if="!qualityOptions.length" class="text-xs text-gray-400 dark:text-white/30 py-1">
        Loading tracks…
      </p>

      <div v-else class="flex flex-col gap-0.5">
        <!-- Auto -->
        <button
          class="flex items-center gap-3 w-full px-2 py-2 rounded-xl text-left transition-all duration-200 ease-out"
          :class="abrEnabled ? 'bg-primary/10' : 'hover:bg-gray-100/60 dark:hover:bg-white/[0.05]'"
          @click="store.selectVariant(null)"
        >
          <span
            class="w-3.5 h-3.5 rounded-full border-2 flex-shrink-0 transition-all duration-200 ease-out"
            :class="
              abrEnabled ? 'border-primary bg-primary' : 'border-gray-300 dark:border-white/20'
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
      <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed select-none mb-3">
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
</template>
