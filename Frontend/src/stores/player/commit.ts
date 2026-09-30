import type { MediaFileDto } from "@/api/streaming";

import type { PlayerState } from "./state";
import type { Order, Origin } from "./types";

export interface CommitOptions {
  position?: number;
  record?: boolean;
  markHeard?: boolean;
}

export interface CommitDeps {
  state: PlayerState;
  order: () => Order | null;
  clearCountdown: () => void;
  recordHistory: (file: MediaFileDto, origin: Origin) => void;
}

export const createCommit = (deps: CommitDeps) => {
  let instanceSequence = 0;

  const commit = (file: MediaFileDto, origin: Origin, options: CommitOptions = {}) => {
    const { state } = deps;
    instanceSequence += 1;
    state.nowPlaying.value = {
      instanceId: instanceSequence,
      file,
      origin,
      ended: false,
      restored: false,
    };
    state.playerDismissed.value = false;
    state.queueEnded.value = false;
    state.clearError();
    deps.clearCountdown();
    if (options.record !== false) deps.recordHistory(file, origin);
    if (origin === "context") {
      state.lastContextFile.value = file;
      if (options.markHeard !== false) state.markHeard(file);
    }
    const active = deps.order();
    if (active && options.position !== undefined) {
      active.prefetch(options.position);
      active.evictAround(options.position);
    }
  };

  const restamp = () => {
    instanceSequence += 1;
    return instanceSequence;
  };

  return { commit, restamp };
};
