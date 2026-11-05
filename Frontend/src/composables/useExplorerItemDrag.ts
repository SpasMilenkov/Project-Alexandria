import { useEventListener } from "@vueuse/core";
import { type Ref, onScopeDispose, readonly, ref } from "vue";

import { type DragItemChip, useExplorerDragSession } from "@/composables/useExplorerDragSession";
import {
  type ExplorerItemKind,
  type Point,
  hasMovedBeyond,
} from "@/features/explorer/drag/geometry";
import { ITEM_SELECTOR } from "@/features/explorer/drag/pointer-gesture";

interface ItemDragOptions {
  container: Ref<HTMLElement | null>;
  enabled: () => boolean;
  isSelected: (id: string, kind: ExplorerItemKind) => boolean;
  selectOnly: (id: string, kind: ExplorerItemKind) => void;
  addToSelection: (id: string, kind: ExplorerItemKind) => void;
  selection: () => { fileIds: string[]; directoryIds: string[] };
  chipFor: (id: string, kind: ExplorerItemKind) => DragItemChip;
  origin: () => { id: string | null; name: string };
  onPickUp?: () => void;
}

interface PendingPress {
  pointerId: number;
  pointerType: string;
  id: string;
  kind: ExplorerItemKind;
  additive: boolean;
  start: Point;
  armedAt: Point | null;
  timer: ReturnType<typeof setTimeout> | null;
}

const MOUSE_THRESHOLD = 4;
const TOUCH_SLOP = 8;
const LONG_PRESS_MS = 400;

/**
 * Turns a press on an explorer item into a drag session. Mouse and pen pick up
 * after a 4px move; touch needs a 400ms hold first so ordinary swipes keep
 * scrolling, and holding still without moving falls through to the long-press
 * context menu.
 */
export const useExplorerItemDrag = (options: ItemDragOptions) => {
  const dragSession = useExplorerDragSession();
  const isArmed = ref(false);
  let pending: PendingPress | null = null;

  const removeListeners = () => {
    window.removeEventListener("pointermove", handlePointerMove);
    window.removeEventListener("pointerup", abandon);
    window.removeEventListener("pointercancel", abandon);
  };

  const abandon = () => {
    if (pending?.timer) clearTimeout(pending.timer);
    pending = null;
    isArmed.value = false;
    removeListeners();
  };

  const pickUp = (press: PendingPress, point: Point) => {
    if (!options.isSelected(press.id, press.kind)) {
      if (press.additive) options.addToSelection(press.id, press.kind);
      else options.selectOnly(press.id, press.kind);
    }

    const { fileIds, directoryIds } = options.selection();
    const origin = options.origin();

    abandon();
    window.getSelection()?.removeAllRanges();
    dragSession.start({
      directoryIds,
      fileIds,
      lead: options.chipFor(press.id, press.kind),
      originId: origin.id,
      originName: origin.name,
      point,
      pointerId: press.pointerId,
      pointerType: press.pointerType,
    });
    options.onPickUp?.();
  };

  const handlePointerMove = (event: PointerEvent) => {
    const press = pending;

    if (!press || event.pointerId !== press.pointerId) return;

    const point = { x: event.clientX, y: event.clientY };

    if (!options.enabled()) {
      abandon();
      return;
    }

    if (press.pointerType !== "touch") {
      if (hasMovedBeyond(press.start, point, MOUSE_THRESHOLD)) pickUp(press, point);
      return;
    }

    if (!press.armedAt) {
      // Moving before the hold completes is a scroll, not a pick-up.
      if (hasMovedBeyond(press.start, point, TOUCH_SLOP)) abandon();
      return;
    }

    if (hasMovedBeyond(press.armedAt, point, MOUSE_THRESHOLD)) pickUp(press, point);
  };

  const arm = () => {
    if (!pending) return;

    pending.timer = null;
    pending.armedAt = pending.start;
    isArmed.value = true;
    navigator.vibrate?.(10);
  };

  const handlePointerDown = (event: PointerEvent) => {
    if (pending || dragSession.isDragging.value) return;
    if (event.button !== 0 || !event.isPrimary || !options.enabled()) return;
    if (!(event.target instanceof Element)) return;

    const row = event.target.closest<HTMLElement>(ITEM_SELECTOR);

    if (!row || !options.container.value?.contains(row)) return;

    const fileId = row.dataset.fileId;
    const directoryId = row.dataset.dirId;
    const id = fileId ?? directoryId;

    if (!id) return;

    pending = {
      additive: event.ctrlKey || event.metaKey,
      armedAt: null,
      id,
      kind: fileId ? "file" : "directory",
      pointerId: event.pointerId,
      pointerType: event.pointerType,
      start: { x: event.clientX, y: event.clientY },
      timer: null,
    };

    if (event.pointerType === "touch") pending.timer = setTimeout(arm, LONG_PRESS_MS);

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", abandon);
    window.addEventListener("pointercancel", abandon);
  };

  // Once a touch is armed or dragging, the page must not pan underneath it.
  // The listener has to exist before the touch starts to be allowed to cancel.
  const handleTouchMove = (event: TouchEvent) => {
    const session = dragSession.session.value;

    if (isArmed.value || session?.pointerType === "touch") event.preventDefault();
  };

  // An armed touch suppresses the native long-press menu event; the context
  // menu's own press timer still opens it if the finger stays still.
  const handleContextMenu = (event: Event) => {
    if (isArmed.value || dragSession.isDragging.value) event.preventDefault();
  };

  // Internal elements (thumbnails, selected text) must never start a native
  // drag: the upload drop zone would read it as an incoming upload.
  const handleDragStart = (event: DragEvent) => {
    event.preventDefault();
  };

  useEventListener(options.container, "pointerdown", handlePointerDown);
  useEventListener(options.container, "touchmove", handleTouchMove, { passive: false });
  useEventListener(options.container, "contextmenu", handleContextMenu, { capture: true });
  useEventListener(options.container, "dragstart", handleDragStart);

  onScopeDispose(abandon);

  return { isArmed: readonly(isArmed), cancelPress: abandon };
};
