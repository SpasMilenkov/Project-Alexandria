import { computed, getCurrentScope, onScopeDispose, readonly, ref, shallowRef } from "vue";

import type { ExplorerItemKind, Point } from "@/features/explorer/drag/geometry";

export interface DragItemChip {
  id: string;
  kind: ExplorerItemKind;
  name: string;
  icon: string;
}

export interface DragSessionInit {
  fileIds: string[];
  directoryIds: string[];
  originId: string | null;
  originName: string;
  lead: DragItemChip;
  pointerId: number;
  pointerType: string;
  point: Point;
}

export interface DragSession extends DragSessionInit {
  count: number;
  sourceIds: ReadonlySet<string>;
}

export type DropKind =
  | "item"
  | "breadcrumb"
  | "background"
  | "panel"
  | "preview-row"
  | "preview-trail";

export type DropInvalidReason = "self" | "origin";

export interface MoveRecord {
  fileIds: string[];
  directoryIds: string[];
  originId: string | null;
  originName: string;
  destinationId: string | null;
  destinationName: string;
  leadName: string;
}

type MoveListener = (record: MoveRecord) => void;

/** `data-drop-dir-id` value standing for the root (Home) folder. */
export const ROOT_DROP_ID = "root";

export const toDropId = (id: string | null): string => id ?? ROOT_DROP_ID;

export const fromDropId = (value: string): string | null => {
  if (value === ROOT_DROP_ID) return null;

  return value;
};

export const dropInvalidReason = (
  targetId: string | null,
  session: Pick<DragSession, "directoryIds" | "originId">,
): DropInvalidReason | null => {
  if (targetId !== null && session.directoryIds.includes(targetId)) return "self";
  if (targetId === session.originId) return "origin";

  return null;
};

// Module scope on purpose: switching explorer tabs remounts the explorer, and a
// drag has to survive that remount to be dropped into the newly opened tab.
const session = shallowRef<DragSession | null>(null);
const pendingMoveCount = ref(0);
const movedListeners = new Set<MoveListener>();
const failedListeners = new Set<MoveListener>();

const subscribe = (listeners: Set<MoveListener>, listener: MoveListener) => {
  listeners.add(listener);

  const unsubscribe = () => {
    listeners.delete(listener);
  };

  if (getCurrentScope()) onScopeDispose(unsubscribe);

  return unsubscribe;
};

const emit = (listeners: Set<MoveListener>, record: MoveRecord) => {
  for (const listener of listeners) listener(record);
};

export const useExplorerDragSession = () => {
  const start = (init: DragSessionInit) => {
    session.value = {
      ...init,
      count: init.fileIds.length + init.directoryIds.length,
      sourceIds: new Set([...init.fileIds, ...init.directoryIds]),
    };
  };

  const end = () => {
    session.value = null;
  };

  const beginMove = (count: number) => {
    pendingMoveCount.value += count;
  };

  const settleMove = (count: number) => {
    pendingMoveCount.value = Math.max(0, pendingMoveCount.value - count);
  };

  return {
    beginMove,
    end,
    isDragging: computed(() => session.value !== null),
    notifyMoveFailed: (record: MoveRecord) => emit(failedListeners, record),
    notifyMoved: (record: MoveRecord) => emit(movedListeners, record),
    onMoveFailed: (listener: MoveListener) => subscribe(failedListeners, listener),
    onMoved: (listener: MoveListener) => subscribe(movedListeners, listener),
    pendingMoveCount: readonly(pendingMoveCount),
    session,
    settleMove,
    start,
  };
};
