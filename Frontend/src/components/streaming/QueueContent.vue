<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { storeToRefs } from "pinia";
import { computed, nextTick, ref, watch } from "vue";

import { usePlayerStore } from "@/stores/stream-player";
import { isNearBottom } from "@/utils/player-shuffle-buffer";

const store = usePlayerStore();
const {
  upNextItems,
  currentIndex,
  activeFile,
  repeatMode,
  shuffled,
  queueEnded,
  userQueue,
  isExpandingSource,
  shufflePosition,
  shuffleTotalCount,
  shuffleSessionId,
  shuffleRestoring,
  shuffleLoadingMore,
  shuffleError,
  shuffleNotice,
  isPlaylistSource,
  activePlaybackOrigin,
  queueStatus,
  parkedContext,
  isAudio,
} = storeToRefs(store);

const userQueueCount = computed(() => userQueue.value.length);
const listRef = ref<HTMLElement | null>(null);

const emit = defineEmits<{ (e: "close"): void }>();

const close = () => emit("close");

watch(currentIndex, () => {
  void nextTick().then(() => scrollToActive(true));
});

watch(shufflePosition, () => {
  void nextTick().then(() => scrollToActive(true));
});

const scrollToActive = (smooth = true) => {
  if (!listRef.value) return;
  const active = listRef.value.querySelector<HTMLElement>("[data-active='true']");
  active?.scrollIntoView({ block: "nearest", behavior: smooth ? "smooth" : "instant" });
};

const onListScroll = (event: Event) => {
  const target = event.target as HTMLElement | null;
  if (!target) return;
  if (!isNearBottom(target.scrollTop, target.clientHeight, target.scrollHeight)) return;
  void store.loadMoreShuffle();
};

const fmtDuration = (secs: number | null): string => {
  if (!secs || !isFinite(secs)) return "--:--";
  const m = Math.floor(secs / 60);
  const s = Math.floor(secs % 60);
  return `${m}:${String(s).padStart(2, "0")}`;
};

defineExpose({ scrollToActive });
</script>

<template>
  <div
    class="queue-content frosted-glass glass-surface-strong border-0 md:border border-black/[0.08] dark:border-white/10 rounded-2xl overflow-hidden shadow-2xl flex h-full min-h-0 max-h-[min(60dvh,480px)] flex-col"
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
          v-if="upNextItems.length"
          class="text-[10px] font-semibold tabular-nums px-1.5 py-0.5 rounded-full bg-black/[0.06] dark:bg-white/[0.08] text-gray-500 dark:text-white/40"
        >
          {{ upNextItems.length }}
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
          v-if="repeatMode !== 'off'"
          class="flex items-center gap-1 text-[10px] font-medium text-primary dark:text-primary bg-primary/10 dark:bg-primary/[0.12] px-1.5 py-0.5 rounded-full ml-1 select-none"
        >
          <Icon :icon="repeatMode === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'" class="w-3 h-3" />
          {{ repeatMode === "one" ? "repeat one" : "loop" }}
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
    <div
      v-if="shuffleNotice"
      class="mx-3 mt-2 mb-1 px-3 py-2 rounded-xl bg-black/[0.04] dark:bg-white/[0.05] flex items-start gap-2"
    >
      <p class="text-[11px] text-gray-600 dark:text-white/60 flex-1">{{ shuffleNotice }}</p>
      <button
        class="w-5 h-5 flex items-center justify-center text-gray-400 dark:text-white/30 hover:text-gray-700 dark:hover:text-white/70 transition-colors"
        title="Dismiss"
        @click.stop="store.dismissShuffleNotice()"
      >
        <Icon icon="mdi:close" class="w-3 h-3" />
      </button>
    </div>
    <ol
      ref="listRef"
      class="overflow-y-auto overscroll-contain flex-1 min-h-0 py-1.5"
      role="listbox"
      @scroll="onListScroll"
    >
      <!-- Playlist / user queue section -->
      <template v-if="userQueueCount > 0">
        <li class="px-5 pt-2 pb-1 select-none flex items-center justify-between">
          <span
            class="text-[10px] font-semibold uppercase tracking-widest text-gray-400 dark:text-white/25"
          >
            Up next
          </span>
          <button
            class="text-[10px] font-medium text-gray-400 dark:text-white/30 hover:text-gray-700 dark:hover:text-white/70 transition-colors"
            title="Clear queue"
            @click.stop="store.clearUserQueue()"
          >
            Clear
          </button>
        </li>
        <li
          v-for="item in upNextItems.filter((i) => i.kind === 'queue')"
          :key="`q-${item.queueIndex}`"
          role="option"
          class="queue-row group relative flex items-center gap-3 px-3 py-2 mx-1.5 rounded-xl cursor-pointer select-none transition-colors hover:bg-black/[0.04] dark:hover:bg-white/[0.04]"
          @click="store.skipToQueueIndex(item.queueIndex)"
        >
          <div class="w-6 flex-shrink-0 flex items-center justify-center">
            <Icon icon="mdi:playlist-play" class="w-3.5 h-3.5 text-primary opacity-60" />
          </div>
          <div class="flex-1 min-w-0">
            <p
              class="text-[13px] font-medium truncate leading-tight text-gray-800 dark:text-white/80"
            >
              {{ item.file.title || item.file.fileName }}
            </p>
            <p
              v-if="item.file.artist"
              class="text-[11px] truncate mt-0.5 text-gray-400 dark:text-white/30"
            >
              {{ item.file.artist }}
            </p>
          </div>
          <button
            class="opacity-0 group-hover:opacity-100 w-5 h-5 flex items-center justify-center text-gray-400 hover:text-red-400 transition-all"
            @click.stop="store.dequeueAt(item.queueIndex)"
          >
            <Icon icon="mdi:close" class="w-3 h-3" />
          </button>
          <span class="text-[11px] tabular-nums flex-shrink-0 text-gray-400 dark:text-white/30">
            {{ fmtDuration(item.file.duration) }}
          </span>
        </li>

        <!-- Divider before source section -->
        <li v-if="upNextItems.some((i) => i.kind === 'source')" class="px-5 pt-3 pb-1 select-none">
          <span
            class="text-[10px] font-semibold uppercase tracking-widest text-gray-400 dark:text-white/25"
          >
            {{ isPlaylistSource ? "Then from playlist" : "Then from library" }}
          </span>
        </li>
        <li
          v-if="isExpandingSource"
          class="flex items-center justify-center gap-2 py-3 text-gray-300 dark:text-white/20"
        >
          <Icon icon="mdi:loading" class="w-3.5 h-3.5 animate-spin" />
          <span class="text-xs">Loading more…</span>
        </li>
      </template>

      <!-- Source list section -->
      <li
        v-for="item in upNextItems.filter((i) => i.kind === 'source')"
        :key="shuffled ? `s-${shuffleSessionId}-${item.sourceIndex}` : `s-${item.sourceIndex}`"
        role="option"
        class="queue-row group relative flex items-center gap-3 px-3 py-2 mx-1.5 rounded-xl cursor-pointer select-none transition-colors hover:bg-black/[0.04] dark:hover:bg-white/[0.04]"
        @click="store.playFromSource(item.sourceIndex)"
      >
        <div class="w-6 flex-shrink-0 flex items-center justify-center">
          <span
            class="text-[11px] tabular-nums text-gray-300 dark:text-white/20 group-hover:hidden"
          >
            {{ item.sourceIndex + 1 }}
          </span>
          <Icon
            icon="mdi:play"
            class="w-3.5 h-3.5 text-gray-400 dark:text-white/40 hidden group-hover:block"
          />
        </div>
        <div class="flex-1 min-w-0">
          <p
            class="text-[13px] font-medium truncate leading-tight text-gray-800 dark:text-white/80"
          >
            {{ item.file.title || item.file.fileName }}
          </p>
          <p
            v-if="item.file.artist"
            class="text-[11px] truncate mt-0.5 text-gray-400 dark:text-white/30"
          >
            {{ item.file.artist }}
          </p>
        </div>
        <span class="text-[11px] tabular-nums flex-shrink-0 text-gray-400 dark:text-white/30">
          {{ fmtDuration(item.file.duration) }}
        </span>
      </li>

      <li
        v-if="shuffleLoadingMore"
        class="flex items-center justify-center gap-2 py-3 text-gray-300 dark:text-white/20"
      >
        <Icon icon="mdi:loading" class="w-3.5 h-3.5 animate-spin" />
        <span class="text-xs">Loading more…</span>
      </li>

      <li
        v-if="shuffleRestoring"
        class="flex items-center justify-center gap-2 py-3 text-gray-300 dark:text-white/20"
      >
        <Icon icon="mdi:loading" class="w-3.5 h-3.5 animate-spin" />
        <span class="text-xs">Reconnecting shuffle…</span>
      </li>

      <li v-if="shuffleError" class="flex flex-col items-center justify-center gap-2 py-4 px-4">
        <span class="text-xs text-gray-500 dark:text-white/40 text-center">{{ shuffleError }}</span>
        <button
          class="text-[11px] font-medium text-primary hover:opacity-80 transition-opacity"
          @click="store.retryShuffle()"
        >
          Retry
        </button>
      </li>

      <li
        v-if="!upNextItems.length && queueStatus === 'loading'"
        class="flex items-center justify-center gap-2 py-10 text-gray-600 dark:text-gray-400"
      >
        <Icon icon="mdi:loading" class="w-5 h-5 animate-spin" />
        <span class="text-xs">Finding what plays next…</span>
      </li>

      <li
        v-else-if="!upNextItems.length && queueStatus === 'ready'"
        class="flex flex-col items-center justify-center gap-2 py-10 text-gray-600 dark:text-gray-400"
      >
        <Icon icon="mdi:playlist-remove" class="w-8 h-8" />
        <span v-if="parkedContext" class="text-xs">Then resume your previous shuffle</span>
        <span v-else-if="isAudio" class="text-xs">Then shuffle your music library</span>
        <span v-else class="text-xs">Nothing queued</span>
      </li>
    </ol>

    <!-- Footer -->
    <div
      v-if="activeFile || queueEnded"
      class="flex items-center gap-2 px-4 py-2.5 border-t border-black/[0.06] dark:border-white/[0.07] flex-shrink-0"
    >
      <template v-if="queueEnded">
        <Icon
          icon="mdi:playlist-check"
          class="w-3.5 h-3.5 flex-shrink-0 text-gray-400 dark:text-white/30"
        />
        <p class="text-[11px] text-gray-600 dark:text-gray-400 flex-1">No playable music found</p>
        <button
          class="text-[11px] font-medium text-primary hover:opacity-80 transition-opacity"
          @click="store.restartQueue()"
        >
          ↺ Replay
        </button>
      </template>
      <template v-else>
        <Icon icon="mdi:music-note" class="w-3.5 h-3.5 flex-shrink-0 text-primary" />

        <p class="text-[11px] text-gray-400 dark:text-white/30 truncate">
          Playing
          <span class="text-gray-600 dark:text-white/60 font-medium">
            {{ activeFile?.title || activeFile?.fileName }}
          </span>
          <template v-if="userQueueCount > 1">
            · {{ currentIndex + 1 }} of {{ userQueueCount }}
          </template>
          <template v-else-if="shuffled && shuffleTotalCount > 0">
            · {{ shuffleTotalCount }} tracks in shuffle
          </template>
        </p>
      </template>
    </div>
  </div>
</template>
