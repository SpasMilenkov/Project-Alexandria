<template>
  <div ref="rootRef" class="contents">
    <!-- Navbar pill -->
    <div
      v-show="presentation === 'pill'"
      ref="pillRef"
      role="button"
      tabindex="0"
      :aria-label="pillLabel"
      class="md:hidden relative flex flex-1 min-w-0 items-center gap-2 h-12 pl-1 pr-2 rounded-full frosted-glass glass-surface border border-gray-200/70 dark:border-gray-700/70 overflow-hidden cursor-pointer"
      @click="openCard"
      @keydown.enter="openCard"
      @keydown.space.prevent="openCard"
    >
      <div class="w-8 h-8 rounded-full overflow-hidden bg-gray-100 dark:bg-gray-800 flex-shrink-0">
        <PlayerArtwork
          :file="activeFile"
          :alt="trackTitle"
          icon-class="w-4 h-4 text-gray-400 dark:text-white/30"
        />
      </div>
      <p class="flex min-w-0 flex-1 flex-col justify-center gap-1 m-0">
        <span class="truncate text-[13px] leading-5 text-gray-900 dark:text-gray-100">
          <span class="font-semibold">{{ trackTitle }}</span>
          <span v-if="trackArtist" class="font-normal text-gray-600 dark:text-gray-400">
            {{ ` · ${trackArtist}` }}
          </span>
        </span>
        <span class="block h-0.5 overflow-hidden rounded bg-gray-500/30" aria-hidden="true">
          <span
            class="block h-full bg-gray-500 dark:bg-gray-400"
            :style="{ width: `${progress}%` }"
          />
        </span>
      </p>
      <button
        class="w-8 h-8 rounded-full flex-shrink-0 flex items-center justify-center bg-primary text-white hover:opacity-90 transition-opacity"
        :aria-label="playPauseLabel"
        @click.stop="store.togglePlay()"
      >
        <Icon v-if="isBuffering" icon="mdi:loading" class="w-4 h-4 animate-spin" />
        <Icon v-else :icon="playPauseIcon" class="w-4 h-4" />
      </button>
    </div>

    <!-- Sheet scrim. Layer scale: page chrome 20 or below, player scrim 30,
      player surfaces 40, dialogs and drawers 50. -->
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      leave-active-class="transition-opacity duration-150 ease-in"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-show="presentation === 'sheet'"
        class="fixed inset-0 z-30 bg-black/30"
        aria-hidden="true"
        @click="presentation = 'pill'"
      />
    </Transition>

    <!-- Expanded card and sheet -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      leave-active-class="transition-all duration-150 ease-in"
      enter-from-class="opacity-0 translate-y-4"
      leave-to-class="opacity-0 translate-y-4"
    >
      <div
        v-show="presentation !== 'pill'"
        class="fixed inset-x-2 bottom-0 z-40 pb-[calc(0.5rem+env(safe-area-inset-bottom))]"
      >
        <div
          ref="sheetRef"
          class="frosted-glass glass-surface-strong border border-gray-200/70 dark:border-gray-700/70 rounded-3xl overflow-hidden flex flex-col"
          role="dialog"
          aria-label="Now playing"
          @keydown="onSheetKeydown"
        >
          <div class="flex items-center gap-4 p-4 pb-2">
            <div
              class="w-12 h-12 rounded-xl overflow-hidden bg-gray-100 dark:bg-gray-800 flex-shrink-0"
            >
              <PlayerArtwork
                :file="activeFile"
                :alt="trackTitle"
                icon-class="w-6 h-6 text-gray-400 dark:text-white/30"
              />
            </div>
            <div class="flex-1 min-w-0">
              <RouterLink
                v-if="trackDetailsTo"
                :to="trackDetailsTo"
                class="block text-sm font-semibold truncate text-gray-900 dark:text-gray-100 m-0"
                :title="trackTitle"
                @click="goToTrackDetails"
              >
                {{ trackTitle }}
              </RouterLink>
              <p v-else class="text-sm font-semibold truncate text-gray-900 dark:text-gray-100 m-0">
                {{ trackTitle }}
              </p>
              <p
                v-if="trackArtist"
                class="text-xs truncate text-gray-600 dark:text-gray-400 m-0"
                :title="trackArtist"
              >
                {{ trackArtist }}
              </p>
            </div>
            <button
              ref="collapseRef"
              class="w-10 h-10 rounded-lg flex-shrink-0 flex items-center justify-center text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 transition-colors"
              title="Collapse"
              aria-label="Collapse player"
              @click="collapse"
            >
              <Icon icon="mdi:chevron-down" class="w-5 h-5" />
            </button>
          </div>

          <div class="flex flex-col gap-1 px-4 py-1">
            <div>
              <input
                type="range"
                class="w-full accent-primary"
                :max="duration || 0"
                :step="0.5"
                :value="currentTime"
                aria-label="Seek"
                @input="onSeekInput"
              />
              <div class="flex justify-between">
                <span class="seek-time text-gray-500 dark:text-gray-500">{{
                  formatTime(currentTime)
                }}</span>
                <span class="seek-time text-gray-500 dark:text-gray-500">{{
                  formatTime(duration)
                }}</span>
              </div>
            </div>

            <div class="flex items-center justify-center gap-2">
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
                :class="shuffleToggleClass"
                :disabled="shuffleBusy"
                :title="shuffleTitle"
                :aria-label="shuffleTitle"
                @click="store.toggleShuffle()"
              >
                <Icon v-if="shuffleBusy" icon="mdi:loading" class="w-5 h-5 animate-spin" />
                <Icon v-else icon="mdi:shuffle-variant" class="w-5 h-5" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-700 dark:text-white/85 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasPrevious"
                title="Previous"
                aria-label="Previous"
                @click="store.previous()"
              >
                <Icon icon="mdi:skip-previous" class="w-6 h-6" />
              </button>
              <button
                class="w-12 h-12 rounded-full flex items-center justify-center bg-primary text-white hover:opacity-90 disabled:opacity-30 disabled:cursor-not-allowed transition-opacity"
                :disabled="!activeFile"
                :title="playPauseLabel"
                :aria-label="playPauseLabel"
                @click="store.togglePlay()"
              >
                <Icon v-if="isBuffering" icon="mdi:loading" class="w-6 h-6 animate-spin" />
                <Icon v-else :icon="playPauseIcon" class="w-6 h-6" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center text-gray-700 dark:text-white/85 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                :disabled="!hasNext"
                title="Next"
                aria-label="Next"
                @click="store.next()"
              >
                <Icon icon="mdi:skip-next" class="w-6 h-6" />
              </button>
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
                :class="repeatToggleClass"
                title="Repeat"
                aria-label="Repeat"
                @click="store.toggleLoop()"
              >
                <Icon :icon="repeatIcon" class="w-5 h-5" />
              </button>
            </div>
          </div>

          <div class="flex items-center gap-1 px-4">
            <button
              v-for="tab in tabs"
              :key="tab.id"
              class="flex-1 h-10 rounded-lg flex items-center justify-center gap-2 text-[13px] transition-colors"
              :class="tabButtonClass(tab.id)"
              :aria-pressed="presentation === 'sheet' && pane === tab.id"
              @click="openSheet(tab.id)"
            >
              <Icon :icon="tab.icon" class="w-4 h-4" />
              {{ tab.label }}
            </button>

            <div ref="volumeWrapRef" class="relative flex-shrink-0">
              <button
                class="w-10 h-10 rounded-lg flex items-center justify-center transition-colors"
                :class="volumeToggleClass"
                :title="muteLabel"
                :aria-label="`Volume, ${Math.round(volume * 100)} percent`"
                aria-haspopup="true"
                :aria-expanded="volumePopoverOpen"
                @click="volumePopoverOpen = !volumePopoverOpen"
              >
                <Icon :icon="volumeIcon" class="w-5 h-5" />
              </button>

              <Transition
                enter-active-class="transition-all duration-150 ease-out"
                leave-active-class="transition-all duration-100 ease-in"
                enter-from-class="opacity-0 translate-y-1"
                leave-to-class="opacity-0 translate-y-1"
              >
                <div
                  v-if="volumePopoverOpen"
                  class="absolute bottom-full right-0 z-10 mb-2 flex w-12 flex-col items-center gap-2 rounded-2xl frosted-glass glass-surface-strong border border-gray-200/70 p-2 shadow-lg dark:border-gray-700/70"
                  role="group"
                  aria-label="Volume control"
                >
                  <input
                    type="range"
                    class="volume-vertical accent-primary"
                    :min="0"
                    :max="1"
                    :step="0.02"
                    :value="volume"
                    aria-label="Volume"
                    @input="onVolumeInput"
                  />
                  <button
                    class="w-8 h-8 flex-shrink-0 flex items-center justify-center text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 transition-colors"
                    :title="muteLabel"
                    :aria-label="muteLabel"
                    @click="toggleMute"
                  >
                    <Icon :icon="volumeIcon" class="w-4 h-4" />
                  </button>
                </div>
              </Transition>
            </div>

            <button
              class="w-10 h-10 rounded-lg flex-shrink-0 flex items-center justify-center text-gray-400 dark:text-white/40 hover:text-red-500 dark:hover:text-red-400 transition-colors"
              title="Close player"
              aria-label="Close player"
              @click="closeIsland"
            >
              <Icon icon="mdi:close" class="w-5 h-5" />
            </button>
          </div>

          <div v-show="presentation === 'sheet'" class="px-2 pb-2">
            <div class="h-[min(52dvh,480px)] min-h-0 overflow-hidden flex flex-col">
              <div v-show="pane === 'lyrics'" class="h-full min-h-0">
                <LyricsContent @close="collapseToCard" />
              </div>
              <div v-show="pane === 'queue'" class="h-full min-h-0">
                <QueueContent @close="collapseToCard" />
              </div>
              <div v-show="pane === 'settings'" class="h-full min-h-0 overflow-y-auto">
                <SettingsContent />
              </div>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

import LyricsContent from "@/components/streaming/LyricsContent.vue";
import PlayerArtwork from "@/components/streaming/PlayerArtwork.vue";
import QueueContent from "@/components/streaming/QueueContent.vue";
import SettingsContent from "@/components/streaming/SettingsContent.vue";
import { usePlayerStore } from "@/stores/stream-player";
import { logger } from "@/utils/logger";

type Presentation = "pill" | "card" | "sheet";
type Pane = "lyrics" | "queue" | "settings";

const store = usePlayerStore();
const {
  activeFile,
  hasNext,
  hasPrevious,
  repeatMode,
  shuffled,
  shuffleBusy,
  isPlaying,
  engineBuffering: isBuffering,
  currentTime,
  duration,
  volume,
  transientEpoch,
  lyricsOpen,
} = storeToRefs(store);

const presentation = ref<Presentation>("pill");
const pane = ref<Pane>("lyrics");

const rootRef = ref<HTMLElement | null>(null);
const sheetRef = ref<HTMLElement | null>(null);
const pillRef = ref<HTMLElement | null>(null);
const collapseRef = ref<HTMLButtonElement | null>(null);
const volumeWrapRef = ref<HTMLElement | null>(null);
const volumePopoverOpen = ref(false);

const trackTitle = computed(
  () => activeFile.value?.title ?? activeFile.value?.fileName ?? "Unknown",
);
const trackArtist = computed(() => activeFile.value?.artist ?? null);
const pillLabel = computed(() => `Open player: ${trackTitle.value}`);
const progress = computed(() => {
  if (!duration.value) return 0;
  return Math.max(0, Math.min(100, (currentTime.value / duration.value) * 100));
});
const shuffleTitle = computed(() => {
  if (shuffleBusy.value) return "Starting shuffle…";
  return shuffled.value ? "Disable shuffle" : "Shuffle this source";
});

const playPauseIcon = computed(() => {
  if (isPlaying.value) return "mdi:pause";
  return "mdi:play";
});

const playPauseLabel = computed(() => {
  if (isPlaying.value) return "Pause";
  return "Play";
});

const repeatIcon = computed(() => {
  if (repeatMode.value === "one") return "mdi:repeat-once";
  return "mdi:repeat";
});

const shuffleToggleClass = computed(() => {
  if (shuffled.value) return "text-primary";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});

const repeatToggleClass = computed(() => {
  if (repeatMode.value === "all" || repeatMode.value === "one") return "text-primary";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});

const muteLabel = computed(() => {
  if (isMuted.value) return "Unmute";
  return "Mute";
});

const volumeToggleClass = computed(() => {
  if (volumePopoverOpen.value) return "text-primary bg-primary/10";
  return "text-gray-400 dark:text-white/40 hover:text-gray-700 dark:hover:text-white/80";
});

const tabButtonClass = (tab: Pane) => {
  if (presentation.value === "sheet" && pane.value === tab) return "text-primary bg-primary/10";
  return "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100";
};
const trackDetailsTo = computed(() =>
  activeFile.value
    ? { name: "track-details" as const, params: { fileId: activeFile.value.fileId } }
    : null,
);

const goToTrackDetails = () => {
  store.closeTransientSurfaces();
};

const tabs = [
  { id: "lyrics", label: "Lyrics", icon: "mdi:script-text-outline" },
  { id: "queue", label: "Queue", icon: "mdi:playlist-play" },
  { id: "settings", label: "Settings", icon: "mdi:tune-variant" },
] as const;

const formatTime = (seconds: number) => {
  if (!isFinite(seconds) || seconds < 0) return "0:00";
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, "0")}`;
};

const onSeekInput = (e: Event) => {
  store.seek(Number((e.target as HTMLInputElement).value));
};

const premuteVolume = ref(1);
const isMuted = computed(() => volume.value === 0);

const volumeIcon = computed(() => {
  if (volume.value === 0) return "mdi:volume-off";
  if (volume.value < 0.5) return "mdi:volume-medium";
  return "mdi:volume-high";
});

const toggleMute = () => {
  if (isMuted.value) {
    store.setVolume(premuteVolume.value > 0 ? premuteVolume.value : 1);
  } else {
    premuteVolume.value = volume.value;
    store.setVolume(0);
  }
};

const onVolumeInput = (e: Event) => {
  store.setVolume(Number((e.target as HTMLInputElement).value));
};

const openCard = () => {
  presentation.value = "card";
  void nextTick().then(() => collapseRef.value?.focus());
};

const openSheet = (tab: Pane) => {
  if (presentation.value === "sheet" && pane.value === tab) {
    presentation.value = "card";
    return;
  }
  pane.value = tab;
  presentation.value = "sheet";
};

const collapse = () => {
  if (presentation.value === "sheet") {
    presentation.value = "card";
    return;
  }
  presentation.value = "pill";
  void nextTick().then(() => pillRef.value?.focus());
};

const collapseToCard = () => {
  presentation.value = "card";
};

const closeIsland = () => {
  presentation.value = "pill";
  store.closePlayer();
};

const onSheetKeydown = (e: KeyboardEvent) => {
  if (e.key !== "Tab" || presentation.value !== "sheet" || !sheetRef.value) return;
  const focusables = sheetRef.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
  );
  if (!focusables.length) return;
  const first = focusables[0];
  const last = focusables[focusables.length - 1];
  if (e.shiftKey && document.activeElement === first) {
    e.preventDefault();
    last.focus();
  } else if (!e.shiftKey && document.activeElement === last) {
    e.preventDefault();
    first.focus();
  }
};

const onDocumentKeydown = (e: KeyboardEvent) => {
  if (e.key !== "Escape" || presentation.value === "pill") return;
  e.stopPropagation();
  if (volumePopoverOpen.value) {
    volumePopoverOpen.value = false;
    return;
  }
  collapse();
};

const onOutsideClick = (e: MouseEvent) => {
  if (
    volumePopoverOpen.value &&
    volumeWrapRef.value &&
    !volumeWrapRef.value.contains(e.target as Node)
  ) {
    volumePopoverOpen.value = false;
  }
  if (presentation.value === "pill" || !rootRef.value) return;
  if (!rootRef.value.contains(e.target as Node)) presentation.value = "pill";
};

const onPageScroll = (e: Event) => {
  if (presentation.value === "pill" || !rootRef.value) return;
  // Scrolls inside the island (lyrics auto-scroll, queue and settings panes)
  // must never collapse it. Only page-level scrolling collapses.
  if (rootRef.value.contains(e.target as Node)) return;
  presentation.value = "pill";
};

onMounted(() => {
  document.addEventListener("keydown", onDocumentKeydown);
  document.addEventListener("click", onOutsideClick, { capture: true });
  document.addEventListener("scroll", onPageScroll, { capture: true, passive: true });
});

// Metadata navigation collapses transient surfaces without touching playback.
watch(transientEpoch, () => {
  presentation.value = "pill";
});

// The music grid's lyrics button toggles shared state with no panel of its
// own on mobile, so the island hosts it in the sheet there. Closing the sheet
// lyrics syncs the toggle back off.
const isMobileViewport = () =>
  typeof window !== "undefined" && window.matchMedia("(max-width: 767px)").matches;

watch(lyricsOpen, (open) => {
  if (!open) {
    if (presentation.value === "sheet" && pane.value === "lyrics") collapseToCard();
    return;
  }
  if (!isMobileViewport()) return;
  if (presentation.value === "sheet" && pane.value === "lyrics") return;
  openSheet("lyrics");
});

watch(presentation, (current, previous) => {
  if (previous === "sheet" && current !== "sheet" && pane.value === "lyrics") {
    store.setLyricsOpen(false);
  }
  volumePopoverOpen.value = false;
});

onBeforeUnmount(() => {
  document.removeEventListener("keydown", onDocumentKeydown);
  document.removeEventListener("click", onOutsideClick, { capture: true });
  document.removeEventListener("scroll", onPageScroll, { capture: true });
});
</script>

<style scoped>
.seek-time {
  font-size: 0.6875rem;
  font-variant-numeric: tabular-nums;
}

.volume-vertical {
  writing-mode: vertical-lr;
  direction: rtl;
  width: 2rem;
  height: 6rem;
  cursor: pointer;
}

@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    transition: none !important;
    animation: none !important;
  }
}
</style>
