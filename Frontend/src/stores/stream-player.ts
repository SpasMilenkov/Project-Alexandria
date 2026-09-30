import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref, shallowRef } from "vue";

import { createAutoplayCountdown } from "./player/autoplay-countdown";
import { createCommit } from "./player/commit";
import { createEngineBridge } from "./player/engine-bridge";
import { createHistory } from "./player/history";
import { createIntents } from "./player/intents";
import { migrateLegacyPlayer } from "./player/migrate";
import { createNavigation } from "./player/navigation";
import { createOrderManager } from "./player/order-manager";
import { createQueue } from "./player/queue";
import { createRecovery } from "./player/recovery";
import { createPlayerState } from "./player/state";
import { createTransitions, type RunOptions } from "./player/transitions";
import { type VariantTrack } from "./player/transport";
import type { MediaKind, Order, UpNextItem } from "./player/types";

const UPCOMING_LIMIT = 30;

export const usePlayerStore = defineStore(
  "player",
  () => {
    const state = createPlayerState();
    let boundaryError: (err: unknown, options: RunOptions) => void = () => undefined;
    let clearBusy: () => void = () => undefined;
    const transitions = createTransitions({
      onError: (err, options) => boundaryError(err, options),
      onSettled: () => clearBusy(),
    });
    const engine = createEngineBridge();
    const historyRevision = ref(0);
    const history = createHistory(100, () => {
      historyRevision.value += 1;
    });
    const queue = createQueue(state.queueEntries);
    const activeOrder = shallowRef<Order | null>(null);
    const order = () => activeOrder.value;
    const setOrder = (next: Order | null) => {
      activeOrder.value = next;
    };

    let countdownAdvance: () => void = () => undefined;
    const countdown = createAutoplayCountdown(() => countdownAdvance());
    const commit = createCommit({
      state,
      order,
      clearCountdown: countdown.clear,
      recordHistory: history.push,
    });
    const orders = createOrderManager({
      state,
      order,
      setOrder,
      flushHistory: () => engine.flushHistory(),
      commit: commit.commit,
    });
    const intents = createIntents({ state, transitions, queue, orders, commit, order });
    const navigation = createNavigation({
      state,
      transitions,
      engine,
      history,
      queue,
      orders,
      commit,
      order,
      resumeCore: intents.resumeCore,
    });

    // Mirrors resolveNext: history, queue, repeat, remaining context, then the
    // end policy for the kind of the track that is playing (not the context).
    const hasNext = computed(() => {
      void historyRevision.value;
      void state.orderRevision.value;
      if (history.hasForward()) return true;
      if (state.queueEntries.value.length > 0) return true;
      if (state.repeatMode.value !== "off") return true;
      const playing = state.nowPlaying.value;
      if (playing && state.mediaKind(playing.file) === "audio" && state.afterContextEnds.value !== "stop") {
        return true;
      }
      const current = state.context.value;
      if (!current) return false;
      const active = order();
      if (!active) return true;
      const total = active.total || current.total;
      if (total <= 0) return true;
      for (let position = current.cursor + 1; position < total; position++) {
        if (!active.isScanned(position)) return true;
        const file = active.at(position);
        if (file && (!current.shuffled || !state.isHeard(file.fileId))) return true;
      }
      return false;
    });

    const recovery = createRecovery({
      state,
      transitions,
      engine,
      history,
      orders,
      commit,
      order,
      countdown,
      next: navigation.next,
      resolveNext: navigation.resolveNext,
      hasNext: () => hasNext.value,
    });
    countdownAdvance = () => {
      void navigation.next("natural");
    };
    boundaryError = recovery.boundaryError;
    clearBusy = recovery.clearBusy;

    migrateLegacyPlayer(state, commit.restamp);

    const activeFile = computed(() => state.nowPlaying.value?.file ?? null);
    const isAudio = computed(() => {
      const file = activeFile.value;
      if (!file) return true;
      return file.mimeType.startsWith("audio/");
    });
    const hasActiveFile = computed(() => state.nowPlaying.value !== null);
    const hasPrevious = computed(() => state.nowPlaying.value !== null);
    const isStrip = computed(() => state.playerMode.value === "strip");
    const isPlaylistSource = computed(
      () => (state.context.value?.ref.playlistId ?? null) !== null,
    );
    const sourceId = computed(() => {
      const ref = state.context.value?.ref ?? null;
      if (!ref) return null;
      if (ref.playlistId) return `playlist:${ref.playlistId}`;
      if (ref.isVideo) return "library:video";
      return "library:audio";
    });
    const playingFrom = computed(() => {
      if (state.nowPlaying.value?.origin === "queue") return "Queue";
      return state.context.value?.label ?? "Queue";
    });
    const queueStatus = computed<"ready" | "loading" | "error" | "empty">(() => {
      if (state.orderBusy.value) return "loading";
      if (state.error.value) return "error";
      if (state.queueEnded.value) return "empty";
      return "ready";
    });
    const upNextItems = computed((): UpNextItem[] => {
      void historyRevision.value;
      void state.orderRevision.value;
      const items: UpNextItem[] = [];
      for (const [queueIndex, entry] of history.forwardEntries().entries()) {
        items.push({ kind: "history", file: entry.file, queueIndex, position: -1, group: null });
      }
      state.queueEntries.value.forEach((entry, queueIndex) => {
        items.push({ kind: "queue", file: entry.file, queueIndex, position: queueIndex, group: entry.group });
      });
      const current = state.context.value;
      const active = order();
      if (current && active) {
        const total = active.total || current.total;
        let collected = 0;
        let scanned = 0;
        for (let position = current.cursor + 1; collected < UPCOMING_LIMIT; position++) {
          if (total > 0 && position >= total) break;
          if (scanned > UPCOMING_LIMIT * 4) break;
          scanned += 1;
          const file = active.at(position);
          if (!file) continue;
          if (current.shuffled && state.isHeard(file.fileId)) continue;
          collected += 1;
          items.push({ kind: "context", file, queueIndex: -1, position, group: null });
        }
      }
      return items;
    });

    const kindOfNowPlaying = (): MediaKind => state.mediaKind(activeFile.value);

    const play = () => {
      state.queueEnded.value = false;
      state.playerDismissed.value = false;
      engine.play(kindOfNowPlaying());
    };
    const pause = () => engine.pause(kindOfNowPlaying());
    const togglePlay = () => {
      if (state.transport.isPlaying.value) pause();
      else play();
    };
    const seek = (seconds: number) => engine.seek(kindOfNowPlaying(), seconds);
    const setVolume = (value: number) => {
      state.transport.volume.value = Math.max(0, Math.min(1, value));
      engine.setVolume(kindOfNowPlaying(), state.transport.volume.value);
    };
    const selectVariant = (id: number | null) => engine.selectVariant(kindOfNowPlaying(), id);
    const setPlaybackRate = (rate: number) => engine.setPlaybackRate(kindOfNowPlaying(), rate);

    const closePlayer = () => {
      countdown.clear();
      pause();
      state.transport.isPlaying.value = false;
      state.playerDismissed.value = true;
    };
    const resumePlayer = () => {
      play();
    };
    const toggleLoop = () => {
      if (state.repeatMode.value === "off") state.repeatMode.value = "all";
      else if (state.repeatMode.value === "all") state.repeatMode.value = "one";
      else state.repeatMode.value = "off";
    };
    const toggleVideoAutoplay = () => {
      state.videoAutoplay.value = !state.videoAutoplay.value;
      if (!state.videoAutoplay.value) countdown.clear();
    };
    const setAfterContextEnds = (value: "stop" | "library" | "resume-previous") => {
      state.afterContextEnds.value = value;
    };
    const setPlayerMode = (mode: "expanded" | "pip" | "strip") => {
      state.playerMode.value = mode;
    };
    const setSnapCorner = (corner: "tl" | "tr" | "bl" | "br") => {
      state.snapCorner.value = corner;
    };
    const setLyricsOpen = (open: boolean) => {
      state.lyricsOpen.value = open;
    };
    const toggleLyrics = () => {
      state.lyricsOpen.value = !state.lyricsOpen.value;
    };
    const closeTransientSurfaces = () => {
      state.transientEpoch.value++;
    };
    const registerVideoSurface = (): (() => void) => {
      state.videoSurfaces.value += 1;
      return () => {
        state.videoSurfaces.value = Math.max(0, state.videoSurfaces.value - 1);
      };
    };
    const retryError = (): Promise<void> => {
      const retry = state.error.value?.retry;
      if (!retry) return Promise.resolve();
      state.clearError();
      return retry();
    };

    return {
      nowPlaying: state.nowPlaying,
      context: state.context,
      queueEntries: state.queueEntries,
      snapshot: state.snapshot,
      shufflePreference: state.shufflePreference,
      afterContextEnds: state.afterContextEnds,
      repeatMode: state.repeatMode,
      videoAutoplay: state.videoAutoplay,
      playerMode: state.playerMode,
      snapCorner: state.snapCorner,
      playerDismissed: state.playerDismissed,
      lyricsOpen: state.lyricsOpen,
      transientEpoch: state.transientEpoch,
      queueEnded: state.queueEnded,
      playbackOwnerId: state.playbackOwnerId,
      orderBusy: state.orderBusy,
      error: state.error,
      notices: state.notices,
      currentTime: state.transport.currentTime,
      duration: state.transport.duration,
      isPlaying: state.transport.isPlaying,
      variantTracks: state.transport.variantTracks,
      activeVariantId: state.transport.activeVariantId,
      abrEnabled: state.transport.abrEnabled,
      playbackRate: state.transport.playbackRate,
      volume: state.transport.volume,
      engineBuffering: state.engineBuffering,
      engineLoadError: state.engineLoadError,
      engineReady: state.engineReady,
      activeFile,
      isAudio,
      hasActiveFile,
      hasPrevious,
      hasNext,
      isStrip,
      isPlaylistSource,
      sourceId,
      playingFrom,
      queueStatus,
      upNextItems,
      autoplayCountdown: countdown.seconds,
      startContext: intents.startContext,
      playTrackInContext: intents.playTrackInContext,
      playNow: intents.playNow,
      jumpTo: intents.jumpTo,
      skipToQueue: intents.skipToQueue,
      addToQueue: intents.addToQueue,
      addPlaylistToQueue: intents.addPlaylistToQueue,
      removeFromQueue: intents.removeFromQueue,
      clearQueue: intents.clearQueue,
      shuffleQueue: intents.shuffleQueue,
      enableShuffle: intents.enableShuffle,
      disableShuffle: intents.disableShuffle,
      toggleShuffle: intents.toggleShuffle,
      next: navigation.next,
      previous: navigation.previous,
      playAgain: navigation.playAgain,
      resumePrevious: intents.resumePrevious,
      handleTrackEnded: recovery.handleTrackEnded,
      reportUnavailable: recovery.reportUnavailable,
      contextSourceChanged: recovery.contextSourceChanged,
      restore: recovery.restore,
      clearOwnerState: recovery.clearOwnerState,
      releaseShuffleSession: recovery.releaseSession,
      setIsPlaying: recovery.setIsPlaying,
      setAfterContextEnds,
      cancelAutoplay: countdown.clear,
      retryError,
      dismissNotices: state.dismissNotices,
      registerEngine: engine.registerEngine,
      registerHistoryFlush: engine.registerHistoryFlush,
      registerVideoSurface,
      videoSurfaces: state.videoSurfaces,
      lastContextFile: state.lastContextFile,
      flushHistory: () => engine.flushHistory(),
      play,
      pause,
      togglePlay,
      seek,
      setVolume,
      selectVariant,
      setPlaybackRate,
      setCurrentTime: (value: number) => {
        state.transport.currentTime.value = value;
      },
      setDuration: (value: number) => {
        state.transport.duration.value = value;
      },
      setVariantTracks: (tracks: VariantTrack[]) => {
        state.transport.variantTracks.value = tracks;
      },
      setActiveVariantId: (id: number | null) => {
        state.transport.activeVariantId.value = id;
      },
      setAbrEnabled: (value: boolean) => {
        state.transport.abrEnabled.value = value;
      },
      setPlaybackRateState: (rate: number) => {
        state.transport.playbackRate.value = rate;
      },
      setEngineBuffering: (value: boolean) => {
        state.engineBuffering.value = value;
      },
      setEngineLoadError: (message: string | null) => {
        state.engineLoadError.value = message;
      },
      setEngineReady: (value: boolean) => {
        state.engineReady.value = value;
      },
      setPlayerMode,
      closePlayer,
      resumePlayer,
      setLyricsOpen,
      toggleLyrics,
      closeTransientSurfaces,
      setSnapCorner,
      toggleLoop,
      toggleVideoAutoplay,
    };
  },
  {
    persist: {
      key: "player-v2",
      pick: [
        "nowPlaying",
        "context",
        "queueEntries",
        "snapshot",
        "lastContextFile",
        "shufflePreference",
        "afterContextEnds",
        "repeatMode",
        "videoAutoplay",
        "volume",
        "playerMode",
        "snapCorner",
        "playerDismissed",
        "playbackOwnerId",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(usePlayerStore, import.meta.hot));
}
