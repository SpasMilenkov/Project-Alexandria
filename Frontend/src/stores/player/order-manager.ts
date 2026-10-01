// oxlint-disable max-lines-per-function max-statements
import { isAuthError } from "@/api/shuffle";
import type { MediaFileDto } from "@/api/streaming";
import {
  type SourceAnchor,
  type SourceDescriptor,
  fetchSequentialPage,
  toAnchor,
} from "@/utils/player-source";

import { createSequentialOrder, locateAnchor } from "./order/sequential";
import {
  type OpenedShuffle,
  isSessionGone,
  openShuffle,
  releaseShuffleSession,
} from "./order/shuffle";
import type { PlayerState } from "./state";
import type { Guard } from "./transitions";
import type { Order, Origin, RangeBufferHooks } from "./types";

export type AdvanceOutcome = "advanced" | "exhausted" | "stopped";

export interface OrderOpenOptions {
  label: string;
  shuffle: boolean;
  anchor?: SourceAnchor | null;
  avoidFirstFileId?: string | null;
  sessionId?: string | null;
  requestId?: string;
}

export interface OrderDeps {
  state: PlayerState;
  order: () => Order | null;
  setOrder: (order: Order | null) => void;
  flushHistory: () => Promise<void>;
  commit: (file: MediaFileDto, origin: Origin, options?: { position?: number }) => void;
}

export const createOrderManager = (deps: OrderDeps) => {
  const { state } = deps;
  let rebuiltOnce = false;

  const armRebuild = () => {
    rebuiltOnce = false;
  };

  const canRebuild = () => {
    if (rebuiltOnce) return false;
    rebuiltOnce = true;
    return true;
  };

  const hooksFor = (): RangeBufferHooks => ({
    onEntries: () => {
      state.orderRevision.value += 1;
    },
    onTotal: (total) => {
      const current = state.context.value;
      if (current && current.sessionId === null) {
        state.context.value = { ...current, total };
      }
    },
    onSession: (sessionId, expiresAt, total) => {
      const current = state.context.value;
      if (current && current.sessionId === sessionId) {
        state.context.value = { ...current, expiresAt, total };
      }
    },
  });

  const setSequential = (ref: SourceDescriptor) => {
    void deps.order()?.dispose();
    deps.setOrder(createSequentialOrder(ref, hooksFor()));
  };

  const adoptShuffle = async (
    opened: OpenedShuffle,
    ref: SourceDescriptor,
    label: string,
    anchor: SourceAnchor | null,
    keepCursor: boolean,
  ) => {
    const previous = deps.order();
    const previousSession = state.context.value?.sessionId ?? null;
    const previousCursor = state.context.value?.cursor ?? -1;
    const previousAnchor = state.context.value?.anchor ?? null;
    deps.setOrder(opened.order);
    opened.order.prefetch(0);
    state.context.value = {
      ref: { ...ref },
      label,
      shuffled: true,
      anchor,
      cursor: keepCursor && !opened.created ? previousCursor : -1,
      sessionId: opened.session.sessionId,
      expiresAt: opened.session.expiresAt,
      total: opened.session.total,
    };
    if (keepCursor && !opened.created && previousAnchor) {
      state.context.value = { ...state.context.value, anchor: previousAnchor };
    }
    await previous?.dispose();
    if (previousSession && previousSession !== opened.session.sessionId) {
      await releaseShuffleSession(previousSession);
    }
  };

  const discardOpened = (opened: OpenedShuffle) => {
    void opened.order.dispose();
    void releaseShuffleSession(opened.session.sessionId);
  };

  const open = async (
    guard: Guard,
    ref: SourceDescriptor,
    options: OrderOpenOptions,
  ): Promise<OpenedShuffle | null> => {
    if (!options.shuffle) {
      const previousSession = state.context.value?.sessionId ?? null;
      const previous = deps.order();
      const fresh = createSequentialOrder(ref, hooksFor());
      deps.setOrder(fresh);
      state.context.value = {
        ref: { ...ref },
        label: options.label,
        shuffled: false,
        anchor: options.anchor ?? null,
        cursor: -1,
        sessionId: null,
        expiresAt: null,
        total: 0,
      };
      await previous?.dispose();
      if (previousSession) await releaseShuffleSession(previousSession);
      return null;
    }
    const opened = options.anchor
      ? await guard(
          openShuffle({
            ref,
            anchor: options.anchor,
            sessionId: options.sessionId ?? null,
            requestId: options.requestId,
            flushHistory: deps.flushHistory,
            hooks: hooksFor(),
          }),
          discardOpened,
        )
      : await guard(
          openShuffle({
            ref,
            avoidFirstFileId: options.avoidFirstFileId ?? null,
            sessionId: options.sessionId ?? null,
            requestId: options.requestId,
            flushHistory: deps.flushHistory,
            hooks: hooksFor(),
          }),
          discardOpened,
        );
    await adoptShuffle(opened, ref, options.label, options.anchor ?? null, options.sessionId != null);
    return opened;
  };

  const replaceShuffle = (
    guard: Guard,
    ref: SourceDescriptor,
    label: string,
    anchor: SourceAnchor | null,
    avoidFirstFileId: string | null,
    requestId?: string,
  ): Promise<OpenedShuffle> =>
    open(guard, ref, { label, shuffle: true, anchor, avoidFirstFileId, requestId }).then((opened) => {
      if (!opened) throw new Error("shuffle order missing");
      return opened;
    });

  const releaseActive = (releaseSession: boolean) => {
    const sessionId = state.context.value?.sessionId ?? null;
    void deps.order()?.dispose();
    deps.setOrder(null);
    if (releaseSession && sessionId) void releaseShuffleSession(sessionId);
    const current = state.context.value;
    if (current && current.sessionId !== null) {
      state.context.value = { ...current, sessionId: null };
    }
  };

  const patch = (
    fields: Partial<{ cursor: number; total: number; anchor: SourceAnchor | null; shuffled: boolean }>,
  ) => {
    const current = state.context.value;
    if (current) state.context.value = { ...current, ...fields };
  };

  const seedAtAnchor = async (guard: Guard, ref: SourceDescriptor, anchor: SourceAnchor) => {
    const lookup = await guard(locateAnchor(ref, anchor));
    const active = deps.order();
    if (!lookup || !active) return null;
    active.seed({
      offset: lookup.offset,
      scannedCount: lookup.items.length,
      total: lookup.total,
      items: lookup.items,
    });
    patch({ cursor: lookup.position, total: lookup.total, anchor });
    return lookup;
  };

  // Missing anchor on start or restore (X6): keep the track as an interrupt and
  // continue from page 1. A source that is gone as well clears the context (PE5).
  const seedFirstPage = async (guard: Guard, ref: SourceDescriptor): Promise<boolean> => {
    let page: Awaited<ReturnType<typeof fetchSequentialPage>> | null = null;
    try {
      page = await guard(fetchSequentialPage(ref, 1));
    } catch (err: unknown) {
      if (!isSessionGone(err)) throw err;
    }
    if (!page) {
      applySourceGone("That source is gone. Kept playing this track.");
      return false;
    }
    deps.order()?.seed({
      offset: 0,
      scannedCount: page.items.length,
      total: page.totalCount,
      items: page.items.map((file, index) => ({ position: index, file })),
    });
    patch({ cursor: -1, total: page.totalCount, anchor: null });
    return true;
  };

  const openSequentialAt = async (
    guard: Guard,
    ref: SourceDescriptor,
    label: string,
    anchor: SourceAnchor | null,
  ): Promise<boolean> => {
    // Look the anchor up before touching anything, so a failure here leaves
    // the current order and session in place.
    const lookup = anchor ? await guard(locateAnchor(ref, anchor)) : null;
    const previousSession = state.context.value?.sessionId ?? null;
    const previous = deps.order();
    const fresh = createSequentialOrder(ref, hooksFor());
    if (lookup) {
      fresh.seed({
        offset: lookup.offset,
        scannedCount: lookup.items.length,
        total: lookup.total,
        items: lookup.items,
      });
    }
    deps.setOrder(fresh);
    state.context.value = {
      ref: { ...ref },
      label,
      shuffled: false,
      anchor: lookup ? anchor : null,
      cursor: lookup ? lookup.position : -1,
      sessionId: null,
      expiresAt: null,
      total: lookup ? lookup.total : 0,
    };
    await previous?.dispose();
    if (previousSession) await releaseShuffleSession(previousSession);
    return lookup !== null;
  };

  const applySourceGone = (message: string) => {
    const playlistId = state.context.value?.ref.playlistId ?? null;
    if (playlistId && state.snapshot.value?.ref.playlistId === playlistId) {
      state.snapshot.value = null;
    }
    releaseActive(true);
    state.context.value = null;
    const playing = state.nowPlaying.value;
    if (playing) state.nowPlaying.value = { ...playing, origin: "interrupt" };
    state.showNotice(message);
  };

  const rebuildShuffle = async (guard: Guard): Promise<boolean> => {
    const current = state.context.value;
    if (!current || !current.shuffled || !canRebuild()) {
      throw new Error("This source is no longer available.");
    }
    state.orderBusy.value = true;
    try {
      const opened = await replaceShuffle(guard, current.ref, current.label, current.anchor, null);
      patch({ cursor: opened.anchored ? 0 : -1, anchor: opened.anchored ? current.anchor : null });
      state.showNotice("Shuffle refreshed. The previous shuffle session expired.");
      return true;
    } catch (err: unknown) {
      if (isAuthError(err)) return false;
      if (isSessionGone(err)) {
        applySourceGone("That source is gone. Kept playing this track.");
        return false;
      }
      throw new Error("This source is no longer available.");
    } finally {
      state.orderBusy.value = false;
    }
  };

  const advanceCursor = async (guard: Guard): Promise<AdvanceOutcome> => {
    if (!state.context.value) return "exhausted";
    if (!deps.order()) throw new Error("This source is no longer available.");
    let position = (state.context.value?.cursor ?? -1) + 1;
    for (;;) {
      const live = state.context.value;
      const active = deps.order();
      if (!live || !active) return "stopped";
      const total = active.total || live.total;
      if (total > 0 && position >= total) return "exhausted";
      let file: MediaFileDto | null = null;
      try {
        file = await guard(active.ensure(position));
      } catch (err: unknown) {
        if (isAuthError(err)) throw err;
        if (live.shuffled && isSessionGone(err)) {
          if (await rebuildShuffle(guard)) {
            position = (state.context.value?.cursor ?? -1) + 1;
            continue;
          }
          return "stopped";
        }
        throw err;
      }
      if (active.total === 0) return "exhausted";
      if (!file) {
        position += 1;
        continue;
      }
      if (live.shuffled && state.isHeard(file.fileId)) {
        position += 1;
        continue;
      }
      deps.commit(file, "context", { position });
      patch({ cursor: position, anchor: toAnchor(live.ref, file) });
      return "advanced";
    }
  };

  return {
    open,
    replaceShuffle,
    releaseActive,
    patch,
    setSequential,
    seedAtAnchor,
    openSequentialAt,
    seedFirstPage,
    advanceCursor,
    rebuildShuffle,
    applySourceGone,
    armRebuild,
  };
};

export type OrderManager = ReturnType<typeof createOrderManager>;
