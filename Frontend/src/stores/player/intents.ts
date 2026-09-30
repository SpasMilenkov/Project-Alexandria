import type { MediaFileDto } from "@/api/streaming";

import { shuffleApi } from "@/api/shuffle";
import {
  type SourceDescriptor,
  fetchPlaylistTracks,
  sameSource,
  toAnchor,
} from "@/utils/player-source";

import type { createCommit } from "./commit";
import type { OrderManager } from "./order-manager";
import type { createQueue } from "./queue";
import type { PlayerState } from "./state";
import type { Guard, createTransitions } from "./transitions";
import type { MediaKind, Order } from "./types";

export interface StartContextOptions {
  label: string;
  shuffle?: boolean;
  startAt?: MediaFileDto | null;
  avoidFirstFileId?: string | null;
  takeSnapshot?: boolean;
}

export interface AddToQueueOptions {
  next?: boolean;
  group?: string | null;
}

export interface IntentDeps {
  state: PlayerState;
  transitions: ReturnType<typeof createTransitions>;
  queue: ReturnType<typeof createQueue>;
  orders: OrderManager;
  commit: ReturnType<typeof createCommit>;
  order: () => Order | null;
}

export const createIntents = (deps: IntentDeps) => {
  const { state, transitions, queue, orders, commit, order } = deps;
  const commitFile = commit.commit;

  const takeSnapshot = (ref: SourceDescriptor) => {
    const current = state.context.value;
    if (!current || current.ref.isVideo || current.cursor < 0) return;
    if (sameSource(current.ref, ref)) return;
    const playing = state.nowPlaying.value;
    const file =
      playing && playing.origin === "context" ? playing.file : state.lastContextFile.value;
    if (!file) return;
    state.snapshot.value = {
      ref: { ...current.ref },
      label: current.label,
      shuffled: current.shuffled,
      file,
    };
  };

  const interruptCurrent = (message: string) => {
    const playing = state.nowPlaying.value;
    if (playing) state.nowPlaying.value = { ...playing, origin: "interrupt" };
    state.showNotice(message);
  };

  const startContextInner = (
    ref: SourceDescriptor,
    options: StartContextOptions,
    requestId: string,
  ): Promise<void> =>
    transitions.replace(
      async (guard) => {
        const kind: MediaKind = ref.isVideo ? "video" : "audio";
        if (options.shuffle !== undefined) {
          state.shufflePreference.value = {
            ...state.shufflePreference.value,
            [kind]: options.shuffle,
          };
        }
        const shuffled = options.shuffle ?? state.shufflePreference.value[kind];
        if (options.takeSnapshot !== false) takeSnapshot(ref);
        orders.armRebuild();
        state.orderBusy.value = true;
        if (options.startAt) commitFile(options.startAt, "context", { markHeard: false });
        const anchor = options.startAt ? toAnchor(ref, options.startAt) : null;
        const opened = await orders.open(guard, ref, {
          label: options.label,
          shuffle: shuffled,
          anchor,
          avoidFirstFileId: options.avoidFirstFileId ?? null,
          requestId,
        });
        // The heard set resets only once the replacement order is open, so a
        // failed start leaves the running cycle intact.
        state.startCycle();
        if (options.startAt) state.markHeard(options.startAt);
        if (shuffled) {
          if (options.startAt) {
            if (opened && opened.anchored) {
              orders.patch({ cursor: 0, anchor });
            } else {
              interruptCurrent("That track is no longer in this source.");
            }
          } else if ((await orders.advanceCursor(guard)) === "exhausted") {
            state.queueEnded.value = true;
            state.setError("Nothing playable here yet.", () => startContext(ref, options));
          }
        } else if (options.startAt && anchor) {
          if (
            !(await orders.seedAtAnchor(guard, ref, anchor)) &&
            (await orders.seedFirstPage(guard, ref))
          ) {
            interruptCurrent("That track is no longer in this source.");
          }
        } else if (!options.startAt && (await orders.advanceCursor(guard)) === "exhausted") {
          state.queueEnded.value = true;
          state.setError("Nothing playable here yet.", () => startContext(ref, options));
        }
        if (state.queueEntries.value.length > 0) {
          state.showNotice("Queue kept. It plays before this context.", {
            label: "Clear",
            run: () => queue.clear(),
          });
        }
      },
      {
        message: "Could not start playback. Check your connection and retry.",
        retry: () => startContextInner(ref, options, requestId),
      },
    );

  const startContext = (ref: SourceDescriptor, options: StartContextOptions): Promise<void> =>
    startContextInner(ref, options, shuffleApi.newRequestId());

  const playTrackInContext = (
    file: MediaFileDto,
    ref: SourceDescriptor,
    label: string,
  ): Promise<void> => {
    const current = state.context.value;
    if (!current || !sameSource(current.ref, ref) || !order()) {
      return startContext(ref, { label, startAt: file });
    }
    if (current.shuffled) {
      return transitions.run(async () => {
        orders.patch({ anchor: toAnchor(ref, file) });
        commitFile(file, "context");
      });
    }
    return transitions.run(
      async (guard) => {
        const anchor = toAnchor(ref, file);
        const lookup = await orders.seedAtAnchor(guard, ref, anchor);
        if (!lookup) {
          state.setError("That track is no longer in this source.", () =>
            playTrackInContext(file, ref, label),
          );
          return;
        }
        commitFile(file, "context", { position: lookup.position });
      },
      {
        message: "Could not play that track. Check your connection and retry.",
        retry: () => playTrackInContext(file, ref, label),
      },
    );
  };

  const playNow = (files: MediaFileDto[]): Promise<void> => {
    const [first, ...rest] = files;
    if (!first) return Promise.resolve();
    return transitions.run(async () => {
      if (rest.length) queue.add(rest, null, true);
      commitFile(first, "interrupt", { markHeard: false });
    });
  };

  const jumpTo = (position: number): Promise<void> =>
    transitions.run(
      async (guard) => {
        const current = state.context.value;
        const active = order();
        if (!current || !active) return;
        const file = await guard(active.ensure(position));
        if (!file) {
          state.setError("That track is no longer available.", () => jumpTo(position));
          return;
        }
        orders.patch({ cursor: position, anchor: toAnchor(current.ref, file) });
        commitFile(file, "context", { position });
      },
      {
        message: "Could not play that track. Check your connection and retry.",
        retry: () => jumpTo(position),
      },
    );

  const skipToQueue = (index: number): Promise<void> =>
    transitions.run(async () => {
      const entry = state.queueEntries.value[index];
      if (!entry) return;
      queue.dropThrough(index);
      commitFile(entry.file, "queue", { markHeard: false });
    });

  const addToQueue = (files: MediaFileDto[], options: AddToQueueOptions = {}): Promise<void> =>
    transitions.run(async () => {
      if (!files.length) return;
      const idle = state.nowPlaying.value === null;
      queue.add(files, options.group ?? null, options.next ?? false);
      if (!idle) return;
      const head = queue.takeHead();
      if (head) commitFile(head.file, "queue", { markHeard: false });
    });

  const addPlaylistToQueue = async (playlistId: string, label?: string | null) => {
    const files = await fetchPlaylistTracks(playlistId);
    await addToQueue(files, { group: label ?? null });
    return files.length;
  };

  const removeFromQueue = (index: number): Promise<void> =>
    transitions.run(async () => {
      queue.removeAt(index);
    });

  const clearQueue = (): Promise<void> =>
    transitions.run(async () => {
      queue.clear();
    });

  const shuffleQueue = (): Promise<void> =>
    transitions.run(async () => {
      queue.shuffle();
    });

  const enableNow = async (
    guard: Guard,
    live: { ref: SourceDescriptor; label: string },
    requestId: string = shuffleApi.newRequestId(),
  ) => {
    state.orderBusy.value = true;
    const playing = state.nowPlaying.value;
    const anchor =
      playing && playing.origin === "context" ? toAnchor(live.ref, playing.file) : null;
    const opened = await orders.replaceShuffle(
      guard,
      live.ref,
      live.label,
      anchor,
      null,
      requestId,
    );
    if (opened.anchored && anchor) {
      orders.patch({ shuffled: true, cursor: 0, anchor });
      return;
    }
    orders.patch({ shuffled: true, cursor: -1, anchor: null });
    // Only a lost anchor interrupts. Queue and interrupt tracks keep their origin.
    if (anchor) interruptCurrent("That track left the source. Shuffling what is here instead.");
  };

  const disableNow = async (guard: Guard, live: { ref: SourceDescriptor; label: string }) => {
    state.orderBusy.value = true;
    const anchor = state.context.value?.anchor ?? null;
    const found = await orders.openSequentialAt(guard, live.ref, live.label, anchor);
    if (anchor && !found)
      state.showNotice("That track left the source. Playing the rest in order.");
  };

  const reconcileShuffle = (guard: Guard, requestId?: string): Promise<void> => {
    const live = state.context.value;
    if (!live) return Promise.resolve();
    const kind: MediaKind = live.ref.isVideo ? "video" : "audio";
    const want = state.shufflePreference.value[kind];
    if (want === live.shuffled) return Promise.resolve();
    if (!want) return disableNow(guard, live);
    return enableNow(guard, live, requestId);
  };

  const enableWith = (requestId: string): Promise<void> => {
    const current = state.context.value;
    if (!current) return Promise.resolve();
    const kind: MediaKind = current.ref.isVideo ? "video" : "audio";
    state.shufflePreference.value = { ...state.shufflePreference.value, [kind]: true };
    orders.armRebuild();
    return transitions.replace((guard) => reconcileShuffle(guard, requestId), {
      message: "Could not start shuffle. Check your connection and retry.",
      retry: () => enableWith(requestId),
    });
  };

  const enableShuffle = (): Promise<void> => enableWith(shuffleApi.newRequestId());

  const disableShuffle = (): Promise<void> => {
    const current = state.context.value;
    if (!current) return Promise.resolve();
    const kind: MediaKind = current.ref.isVideo ? "video" : "audio";
    state.shufflePreference.value = { ...state.shufflePreference.value, [kind]: false };
    return transitions.replace((guard) => reconcileShuffle(guard), {
      message: "Could not restore the normal order. Retry to try again.",
      retry: disableShuffle,
    });
  };

  const toggleShuffle = (): Promise<void> => {
    const current = state.context.value;
    let kind: MediaKind = state.mediaKind(state.nowPlaying.value?.file ?? null);
    if (current) {
      if (current.ref.isVideo) kind = "video";
      else kind = "audio";
    }
    const want = !state.shufflePreference.value[kind];
    // Delegate so a failed toggle retries its own target instead of flipping back.
    if (current) return want ? enableShuffle() : disableShuffle();
    state.shufflePreference.value = { ...state.shufflePreference.value, [kind]: want };
    return Promise.resolve();
  };

  const resumeCore = async (
    guard: Guard,
    requestId: string = shuffleApi.newRequestId(),
  ): Promise<boolean> => {
    const snapshot = state.snapshot.value;
    if (!snapshot) return false;
    orders.armRebuild();
    state.orderBusy.value = true;
    commitFile(snapshot.file, "context", { markHeard: false });
    const anchor = toAnchor(snapshot.ref, snapshot.file);
    const opened = snapshot.shuffled
      ? await orders.open(guard, snapshot.ref, {
          label: snapshot.label,
          shuffle: true,
          anchor,
          requestId,
        })
      : null;
    const found =
      snapshot.shuffled ||
      (await orders.openSequentialAt(guard, snapshot.ref, snapshot.label, anchor));
    // A missing anchor falls back to page 1 (X6). This can still fail, so it
    // runs before the snapshot is consumed.
    const recovered =
      !snapshot.shuffled && !found && (await orders.seedFirstPage(guard, snapshot.ref));
    // Consumed only after the order is open, so a failed resume stays retryable.
    state.snapshot.value = null;
    state.startCycle();
    state.markHeard(snapshot.file);
    const kind: MediaKind = snapshot.ref.isVideo ? "video" : "audio";
    state.shufflePreference.value = { ...state.shufflePreference.value, [kind]: snapshot.shuffled };
    if (snapshot.shuffled) {
      if (opened && opened.anchored) {
        orders.patch({ cursor: 0, anchor });
      } else {
        orders.patch({ cursor: -1, anchor: null });
        interruptCurrent("That track is no longer in this source.");
      }
    } else if (recovered) {
      interruptCurrent("That track is no longer in this source.");
    }
    if (state.queueEntries.value.length > 0) {
      state.showNotice("Queue kept. It plays before this context.", {
        label: "Clear",
        run: () => queue.clear(),
      });
    }
    return true;
  };

  const resumeWith = (requestId: string): Promise<void> =>
    transitions.run(
      async (guard) => {
        if (!(await resumeCore(guard, requestId))) {
          state.showNotice("Nothing to resume.");
        }
      },
      {
        message: "Could not resume. Check your connection and retry.",
        retry: () => resumeWith(requestId),
      },
    );

  const resumePrevious = (): Promise<void> => resumeWith(shuffleApi.newRequestId());

  return {
    startContext,
    playTrackInContext,
    playNow,
    jumpTo,
    skipToQueue,
    addToQueue,
    addPlaylistToQueue,
    removeFromQueue,
    clearQueue,
    shuffleQueue,
    enableShuffle,
    disableShuffle,
    toggleShuffle,
    resumeCore,
    resumePrevious,
  };
};

export type PlayerIntents = ReturnType<typeof createIntents>;
