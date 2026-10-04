import { AUDIO_LIBRARY_LABEL, AUDIO_LIBRARY_REF } from "@/utils/player-source";

import type { createCommit } from "./commit";
import type { createEngineBridge } from "./engine-bridge";
import type { createHistory } from "./history";
import type { OrderManager } from "./order-manager";
import type { createQueue } from "./queue";
import type { PlayerState } from "./state";
import type { Guard, createTransitions } from "./transitions";
import type { AdvancementReason, MediaKind, Order } from "./types";

import { PREVIOUS_RESTART_SECONDS } from "./transport";

export interface NavigationDeps {
  state: PlayerState;
  transitions: ReturnType<typeof createTransitions>;
  engine: ReturnType<typeof createEngineBridge>;
  history: ReturnType<typeof createHistory>;
  queue: ReturnType<typeof createQueue>;
  orders: OrderManager;
  commit: ReturnType<typeof createCommit>;
  order: () => Order | null;
  resumeCore: (guard: Guard) => Promise<boolean>;
}

export const createNavigation = (deps: NavigationDeps) => {
  const { state, transitions, engine, history, queue, orders, commit, resumeCore } = deps;
  const commitFile = commit.commit;

  const fileKind = (): MediaKind => state.mediaKind(state.nowPlaying.value?.file ?? null);

  const restartCurrent = () => {
    const kind = fileKind();
    engine.seek(kind, 0);
    engine.play(kind);
  };

  const stopEmpty = (retry: () => Promise<void>) => {
    state.queueEnded.value = true;
    state.setError("Nothing playable here yet.", retry);
  };

  const startLibraryShuffle = async (guard: Guard, avoidFirstFileId: string | null) => {
    state.startCycle();
    await orders.open(guard, AUDIO_LIBRARY_REF, {
      label: AUDIO_LIBRARY_LABEL,
      shuffle: true,
      avoidFirstFileId,
    });
    if ((await orders.advanceCursor(guard)) === "advanced") {
      state.showNotice("Finished. Continuing with a fresh library shuffle.");
      return true;
    }
    return false;
  };

  const startFreshCycle = async (guard: Guard) => {
    const current = state.context.value;
    if (!current) return false;
    const avoid = state.nowPlaying.value?.file.fileId ?? null;
    state.startCycle();
    if (!current.shuffled) {
      orders.patch({ cursor: -1 });
      return (await orders.advanceCursor(guard)) === "advanced";
    }
    await orders.replaceShuffle(guard, current.ref, current.label, null, avoid);
    return (await orders.advanceCursor(guard)) === "advanced";
  };

  const applyEndPolicy = async (guard: Guard, kind: MediaKind): Promise<void> => {
    if (kind === "video") {
      state.queueEnded.value = true;
      return;
    }
    if (state.afterContextEnds.value === "stop") {
      state.queueEnded.value = true;
      return;
    }
    const avoid = state.nowPlaying.value?.file.fileId ?? null;
    if (state.afterContextEnds.value === "resume-previous" && state.snapshot.value) {
      if (await resumeCore(guard)) return;
    }
    if (await startLibraryShuffle(guard, avoid)) return;
    state.queueEnded.value = true;
    state.setError("No playable music is available in your library.", () => next("natural"));
  };

  const resolveNext = async (
    reason: AdvancementReason,
    guard: Guard,
    kind: MediaKind,
  ): Promise<void> => {
    if (reason === "natural" && state.repeatMode.value === "one" && state.nowPlaying.value) {
      restartCurrent();
      return;
    }
    const forward = history.stepForward();
    if (forward) {
      commitFile(forward.file, forward.origin, { record: false, markHeard: false });
      return;
    }
    const head = queue.takeHead();
    if (head) {
      commitFile(head.file, "queue", { markHeard: false });
      return;
    }
    if (!state.context.value) {
      await applyEndPolicy(guard, kind);
      return;
    }
    const outcome = await orders.advanceCursor(guard);
    if (outcome !== "exhausted") return;
    if (state.repeatMode.value === "all" && state.context.value) {
      if (await startFreshCycle(guard)) return;
      stopEmpty(() => next(reason));
      return;
    }
    if (!state.context.value) return;
    await applyEndPolicy(guard, kind);
  };

  const next = (reason: AdvancementReason = "explicit", kind?: MediaKind): Promise<void> => {
    orders.armRebuild();
    const resolved = kind ?? fileKind();
    return transitions.run((guard) => resolveNext(reason, guard, resolved), {
      message: "Could not advance. Check your connection and retry.",
      retry: () => next(reason, kind),
    });
  };

  const previous = (): Promise<void> =>
    // oxlint-disable-next-line require-await
    transitions.run(async () => {
      if (state.transport.currentTime.value > PREVIOUS_RESTART_SECONDS) {
        restartCurrent();
        return;
      }
      const back = history.stepBack();
      if (!back) {
        restartCurrent();
        return;
      }
      commitFile(back.file, back.origin, { record: false, markHeard: false });
    });

  const playAgain = (): Promise<void> => {
    orders.armRebuild();
    return transitions.replace(
      async (guard) => {
        const current = state.context.value;
        if (!current) {
          const head = queue.takeHead();
          if (head) commitFile(head.file, "queue", { markHeard: false });
          return;
        }
        state.queueEnded.value = false;
        state.clearError();
        state.startCycle();
        if (current.shuffled) {
          await orders.open(guard, current.ref, { label: current.label, shuffle: true });
        } else {
          orders.patch({ cursor: -1 });
        }
        if ((await orders.advanceCursor(guard)) !== "advanced") stopEmpty(playAgain);
      },
      {
        message: "Could not start a new cycle. Check your connection and retry.",
        retry: playAgain,
      },
    );
  };

  return { next, previous, playAgain, resolveNext, fileKind };
};

export type PlayerNavigation = ReturnType<typeof createNavigation>;
