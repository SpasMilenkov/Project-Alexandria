import { isAuthError } from "@/api/shuffle";

import type { createAutoplayCountdown } from "./autoplay-countdown";
import type { createCommit } from "./commit";
import type { createEngineBridge } from "./engine-bridge";
import type { createHistory } from "./history";
import { isSessionGone } from "./order/shuffle";
import type { OrderManager } from "./order-manager";
import type { PlayerState } from "./state";
import type { Guard, RunOptions, createTransitions } from "./transitions";
import type { AdvancementReason, MediaKind, Order } from "./types";

const MAX_UNAVAILABLE_STREAK = 5;

export interface RecoveryDeps {
  state: PlayerState;
  transitions: ReturnType<typeof createTransitions>;
  engine: ReturnType<typeof createEngineBridge>;
  history: ReturnType<typeof createHistory>;
  orders: OrderManager;
  commit: ReturnType<typeof createCommit>;
  order: () => Order | null;
  countdown: ReturnType<typeof createAutoplayCountdown>;
  next: (reason?: AdvancementReason, kind?: MediaKind) => Promise<void>;
  resolveNext: (reason: AdvancementReason, guard: Guard, kind: MediaKind) => Promise<void>;
  hasNext: () => boolean;
}

export const createRecovery = (deps: RecoveryDeps) => {
  const { state, transitions, engine, history, orders, commit, order, countdown } = deps;
  let unavailableStreak = 0;

  const fileKind = (): MediaKind => state.mediaKind(state.nowPlaying.value?.file ?? null);

  const boundaryError = (err: unknown, options: RunOptions) => {
    if (isAuthError(err)) return;
    state.setError(options.message ?? "Something went wrong.", options.retry ?? (() => Promise.resolve()));
  };

  const clearBusy = () => {
    state.orderBusy.value = false;
  };

  const handleTrackEnded = (instanceId: number): Promise<void> => {
    const current = state.nowPlaying.value;
    if (!current || current.instanceId !== instanceId || current.ended) {
      return Promise.resolve();
    }
    current.ended = true;
    const kind = fileKind();
    if (state.repeatMode.value === "one") {
      current.ended = false;
      engine.seek(kind, 0);
      engine.play(kind);
      return Promise.resolve();
    }
    if (kind === "audio") return deps.next("natural", kind);
    if (!state.videoAutoplay.value || !deps.hasNext()) return Promise.resolve();
    countdown.start();
    return Promise.resolve();
  };

  const reportUnavailable = (instanceId: number): Promise<void> => {
    orders.armRebuild();
    return transitions.run(async (guard) => {
      const current = state.nowPlaying.value;
      if (!current || current.instanceId !== instanceId) return;
      state.markHeard(current.file);
      unavailableStreak += 1;
      if (unavailableStreak >= MAX_UNAVAILABLE_STREAK) {
        state.queueEnded.value = true;
        state.setError("Several tracks in a row could not be played.", () =>
          deps.next("explicit", fileKind()),
        );
        return;
      }
      // Already inside a transition: next() would queue behind this very task.
      // Explicit, not natural: Repeat one must not restart a track that cannot load.
      await deps.resolveNext("explicit", guard, fileKind());
    }, {
      message: "Could not advance. Check your connection and retry.",
      retry: () => deps.next("explicit"),
    });
  };

  const contextSourceChanged = (
    playlistId: string,
    change: { deleted?: boolean; removedItemId?: string },
  ): Promise<void> => {
    if (change.deleted && state.snapshot.value?.ref.playlistId === playlistId) {
      state.snapshot.value = null;
    }
    const current = state.context.value;
    if (!current || current.ref.playlistId !== playlistId) return Promise.resolve();
    if (change.deleted) {
      return transitions.replace(async () => {
        orders.applySourceGone("That playlist was deleted. Kept playing this track.");
      });
    }
    if (current.shuffled) {
      if (change.removedItemId) {
        const active = order();
        const removed = active?.locate((file) => file.playlistItemId === change.removedItemId) ?? null;
        if (removed) state.markHeard(removed);
      }
      return Promise.resolve();
    }
    return transitions.replace(async (guard) => {
      const live = state.context.value;
      if (!live || !live.anchor) return;
      const previousCursor = live.cursor;
      orders.releaseActive(false);
      orders.setSequential(live.ref);
      if (await orders.seedAtAnchor(guard, live.ref, live.anchor)) return;
      orders.patch({ cursor: Math.max(-1, previousCursor - 1), anchor: null });
    }, {
      message: "Could not refresh that playlist. Check your connection and retry.",
    });
  };

  const restore = (ownerId: string | null): Promise<void> => {
    const previousOwner = state.playbackOwnerId.value;
    if (previousOwner && ownerId && previousOwner !== ownerId) {
      clearOwnerState();
      return Promise.resolve();
    }
    state.playbackOwnerId.value = ownerId;
    if (!ownerId || state.restoredFor.value === ownerId) return Promise.resolve();
    const current = state.context.value;
    const playing = state.nowPlaying.value;
    if (!playing) {
      state.restoredFor.value = ownerId;
      return Promise.resolve();
    }
    if (!current) {
      state.nowPlaying.value = { ...playing, instanceId: commit.restamp(), restored: true };
      state.restoredFor.value = ownerId;
      return Promise.resolve();
    }
    orders.armRebuild();
    return transitions.replace(async (guard) => {
      state.nowPlaying.value = { ...playing, instanceId: commit.restamp(), restored: true };
      const expired =
        current.expiresAt !== null && new Date(current.expiresAt).getTime() <= Date.now();
      if (current.shuffled) {
        if (current.sessionId && !expired) {
          try {
            await orders.open(guard, current.ref, {
              label: current.label,
              shuffle: true,
              sessionId: current.sessionId,
            });
            state.restoredFor.value = ownerId;
            return;
          } catch (err: unknown) {
            if (isAuthError(err)) return;
            if (!isSessionGone(err)) throw err;
          }
        }
        if (await orders.rebuildShuffle(guard)) {
          state.restoredFor.value = ownerId;
        } else if (state.context.value === null) {
          state.restoredFor.value = ownerId;
        }
        return;
      }
      orders.releaseActive(false);
      orders.setSequential(current.ref);
      if (!current.anchor) {
        state.restoredFor.value = ownerId;
        return;
      }
      if (await orders.seedAtAnchor(guard, current.ref, current.anchor)) {
        state.restoredFor.value = ownerId;
        return;
      }
      if (await orders.seedFirstPage(guard, current.ref)) {
        const live = state.nowPlaying.value;
        if (live) state.nowPlaying.value = { ...live, origin: "interrupt" };
        state.showNotice("That track left the source. Playing the rest in order.");
      }
      state.restoredFor.value = ownerId;
    }, {
      message: "Could not restore playback. Retry when ready.",
      retry: () => restore(ownerId),
    });
  };

  const clearOwnerState = () => {
    transitions.supersede();
    const kind = fileKind();
    orders.releaseActive(true);
    state.nowPlaying.value = null;
    state.context.value = null;
    state.snapshot.value = null;
    state.queueEntries.value = [];
    state.playerDismissed.value = false;
    state.lyricsOpen.value = false;
    state.queueEnded.value = false;
    state.playbackOwnerId.value = null;
    state.restoredFor.value = null;
    state.lastContextFile.value = null;
    state.shufflePreference.value = { audio: false, video: false };
    state.afterContextEnds.value = "library";
    state.repeatMode.value = "off";
    state.transport.resetPosition();
    state.startCycle();
    state.clearError();
    state.dismissNotices();
    history.clear();
    countdown.clear();
    engine.pause(kind);
  };

  const releaseSession = () => {
    orders.releaseActive(true);
  };

  const setIsPlaying = (value: boolean) => {
    state.transport.isPlaying.value = value;
    if (!value) return;
    countdown.clear();
    unavailableStreak = 0;
    const current = state.nowPlaying.value;
    if (current?.restored) state.nowPlaying.value = { ...current, restored: false };
  };

  return {
    handleTrackEnded,
    reportUnavailable,
    contextSourceChanged,
    restore,
    clearOwnerState,
    releaseSession,
    setIsPlaying,
    boundaryError,
    clearBusy,
  };
};

export type PlayerRecovery = ReturnType<typeof createRecovery>;
