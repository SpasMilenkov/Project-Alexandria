import { useEventListener } from "@vueuse/core";
import { type Ref, onScopeDispose, readonly, ref, shallowRef } from "vue";

import { useEdgeAutoScroll } from "@/composables/useEdgeAutoScroll";
import {
  type Box,
  type ItemBox,
  type MarqueeMode,
  type Point,
  type SelectionSets,
  hasMovedBeyond,
  itemsInMarquee,
  nextSelection,
  normalizeRect,
  sameMembers,
} from "@/features/explorer/drag/geometry";
import {
  INTERACTIVE_SELECTOR,
  ITEM_SELECTOR,
  suppressNextClick,
} from "@/features/explorer/drag/pointer-gesture";

interface MarqueeOptions {
  container: Ref<HTMLElement | null>;
  selectedFiles: Ref<Set<string>>;
  selectedDirectories: Ref<Set<string>>;
  enabled?: () => boolean;
  onStart?: () => void;
}

interface MarqueeGesture {
  pointerId: number;
  startClient: Point;
  startContent: Point;
  client: Point;
  mode: MarqueeMode;
  base: SelectionSets;
  items: ItemBox[];
  active: boolean;
}

const START_THRESHOLD = 4;

const modeFor = (event: PointerEvent): MarqueeMode => {
  if (event.ctrlKey || event.metaKey) return "toggle";
  if (event.shiftKey) return "add";

  return "replace";
};

/**
 * Rubber-band selection over the explorer listing. Item rectangles are measured
 * once per gesture in scroll-content coordinates, so scrolling never forces a
 * re-measure, and the selection is recomputed at most once per animation frame
 * and only written back when its membership actually changes.
 */
export const useExplorerMarquee = (options: MarqueeOptions) => {
  const marqueeBox = ref<Box | null>(null);
  const isSelecting = ref(false);
  const gesture = shallowRef<MarqueeGesture | null>(null);

  const toContent = (container: HTMLElement, point: Point): Point => {
    const bounds = container.getBoundingClientRect();

    return {
      x: point.x - bounds.left + container.scrollLeft - container.clientLeft,
      y: point.y - bounds.top + container.scrollTop - container.clientTop,
    };
  };

  const measureItems = (container: HTMLElement): ItemBox[] => {
    const bounds = container.getBoundingClientRect();
    const offsetX = container.scrollLeft - bounds.left - container.clientLeft;
    const offsetY = container.scrollTop - bounds.top - container.clientTop;
    const boxes: ItemBox[] = [];

    for (const element of container.querySelectorAll<HTMLElement>(ITEM_SELECTOR)) {
      const fileId = element.dataset.fileId;
      const directoryId = element.dataset.dirId;
      const rect = element.getBoundingClientRect();
      const isRendered = rect.width > 0 || rect.height > 0;
      const box = {
        bottom: rect.bottom + offsetY,
        left: rect.left + offsetX,
        right: rect.right + offsetX,
        top: rect.top + offsetY,
      };

      if (isRendered && fileId) boxes.push({ ...box, id: fileId, kind: "file" });
      else if (isRendered && directoryId)
        boxes.push({ ...box, id: directoryId, kind: "directory" });
    }

    return boxes;
  };

  const applySelection = () => {
    const current = gesture.value;
    const container = options.container.value;

    if (!current?.active || !container) return;

    const box = normalizeRect(current.startContent, toContent(container, current.client));
    const previous = marqueeBox.value;

    if (
      !previous ||
      previous.left !== box.left ||
      previous.top !== box.top ||
      previous.right !== box.right ||
      previous.bottom !== box.bottom
    ) {
      marqueeBox.value = box;
    }

    const next = nextSelection(current.base, itemsInMarquee(current.items, box), current.mode);

    if (!sameMembers(next.files, options.selectedFiles.value)) {
      options.selectedFiles.value = next.files;
    }

    if (!sameMembers(next.directories, options.selectedDirectories.value)) {
      options.selectedDirectories.value = next.directories;
    }
  };

  const autoScroll = useEdgeAutoScroll(applySelection);

  const activate = (current: MarqueeGesture, container: HTMLElement) => {
    current.active = true;
    current.items = measureItems(container);
    isSelecting.value = true;
    window.getSelection()?.removeAllRanges();
    options.onStart?.();
    autoScroll.start(current.client, container);
  };

  const removeWindowListeners = () => {
    window.removeEventListener("pointermove", handlePointerMove);
    window.removeEventListener("pointerup", handlePointerUp);
    window.removeEventListener("pointercancel", handlePointerCancel);
    window.removeEventListener("keydown", handleKeydown, true);
  };

  const finish = (completed: boolean) => {
    const current = gesture.value;

    removeWindowListeners();
    autoScroll.stop();
    gesture.value = null;
    marqueeBox.value = null;
    isSelecting.value = false;

    if (!current?.active) return;

    if (completed) {
      suppressNextClick();
      return;
    }

    options.selectedFiles.value = new Set(current.base.files);
    options.selectedDirectories.value = new Set(current.base.directories);
  };

  const handlePointerMove = (event: PointerEvent) => {
    const current = gesture.value;
    const container = options.container.value;

    if (!current || event.pointerId !== current.pointerId || !container) return;

    current.client = { x: event.clientX, y: event.clientY };

    if (!current.active) {
      if (!hasMovedBeyond(current.startClient, current.client, START_THRESHOLD)) return;
      activate(current, container);
    }

    autoScroll.update(current.client);
  };

  const handlePointerUp = (event: PointerEvent) => {
    if (event.pointerId !== gesture.value?.pointerId) return;
    finish(true);
  };

  const handlePointerCancel = (event: PointerEvent) => {
    if (event.pointerId !== gesture.value?.pointerId) return;
    finish(false);
  };

  // Escape restores the selection from before the gesture.
  const handleKeydown = (event: KeyboardEvent) => {
    if (event.key !== "Escape" || !gesture.value?.active) return;
    event.preventDefault();
    event.stopPropagation();
    finish(false);
  };

  const isOnScrollbar = (container: HTMLElement, event: PointerEvent) => {
    if (event.target !== container) return false;

    const bounds = container.getBoundingClientRect();

    return (
      event.clientX - bounds.left >= container.clientLeft + container.clientWidth ||
      event.clientY - bounds.top >= container.clientTop + container.clientHeight
    );
  };

  const handlePointerDown = (event: PointerEvent) => {
    const container = options.container.value;

    if (!container || gesture.value) return;
    if (event.pointerType === "touch" || event.button !== 0 || !event.isPrimary) return;
    if (options.enabled && !options.enabled()) return;
    if (!(event.target instanceof Element)) return;
    if (event.target.closest(ITEM_SELECTOR) || event.target.closest(INTERACTIVE_SELECTOR)) return;
    if (isOnScrollbar(container, event)) return;

    // Background presses would otherwise start a text selection across tiles.
    event.preventDefault();

    const client = { x: event.clientX, y: event.clientY };

    gesture.value = {
      active: false,
      base: {
        directories: new Set(options.selectedDirectories.value),
        files: new Set(options.selectedFiles.value),
      },
      client,
      items: [],
      mode: modeFor(event),
      pointerId: event.pointerId,
      startClient: client,
      startContent: toContent(container, client),
    };

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", handlePointerUp);
    window.addEventListener("pointercancel", handlePointerCancel);
    window.addEventListener("keydown", handleKeydown, true);
  };

  useEventListener(options.container, "pointerdown", handlePointerDown);

  onScopeDispose(removeWindowListeners);

  return {
    isSelecting: readonly(isSelecting),
    marqueeBox: readonly(marqueeBox),
  };
};
