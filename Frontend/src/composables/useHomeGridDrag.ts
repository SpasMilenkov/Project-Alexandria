import { useEventListener, useResizeObserver } from "@vueuse/core";
import { computed, onUnmounted, ref, shallowRef, watch } from "vue";

import type { HomeLayout, HomeWidget } from "@/types/home";

import { previewHomePlacement } from "@/utils/home-grid";
import {
  type HomeResizeEdge,
  homeCanvasContains,
  homeDragFromControl,
  homePointerPosition,
  homePointerResize,
  scrollHomeGrid,
} from "@/utils/home-grid-input";

interface GridDragOptions {
  board: () => HTMLElement | null;
  layout: () => HomeLayout;
  enabled: () => boolean;
  select: (instanceId: string) => void;
  place: (widget: HomeWidget) => void;
  announce: (message: string) => void;
}

interface PointerSession {
  pointerId: number;
  widget: HomeWidget;
  start: { x: number; y: number };
  offset: { x: number; y: number };
  edge: HomeResizeEdge | null;
  startBoardPoint: { x: number; y: number };
}

export const useHomeGridDrag = (options: GridDragOptions) => {
  const session = shallowRef<PointerSession | null>(null);
  const candidate = shallowRef<HomeWidget | null>(null);
  const active = ref(false);
  const inside = ref(false);
  let frame = 0;
  let point = { x: 0, y: 0 };
  let boardWidth = 0;

  const resizing = computed(() => Boolean(session.value?.edge));

  const preview = computed(() => {
    if (!candidate.value || !inside.value || !active.value) return null;

    return previewHomePlacement(options.layout(), candidate.value);
  });

  const cancel = () => {
    cancelAnimationFrame(frame);
    frame = 0;
    session.value = null;
    candidate.value = null;
    active.value = false;
    inside.value = false;
  };

  const updatePosition = () => {
    const board = options.board();
    const source = session.value;
    if (!board || !source) return;

    const bounds = board.getBoundingClientRect();
    inside.value = Boolean(source.edge) || homeCanvasContains(board, point);
    if (source.edge) {
      candidate.value = homePointerResize(
        bounds,
        options.layout().columns,
        source.widget,
        source.edge,
        { point, startBoardPoint: source.startBoardPoint },
      );

      return;
    }

    candidate.value = homePointerPosition(
      bounds,
      options.layout().columns,
      source.widget,
      point,
      source.offset,
    );
  };

  const scrollFrame = () => {
    const board = options.board();
    if (!active.value || !board) return;

    scrollHomeGrid(board, point);
    updatePosition();
    frame = requestAnimationFrame(scrollFrame);
  };

  const start = (
    widget: HomeWidget,
    event: PointerEvent,
    catalogue = false,
    edge: HomeResizeEdge | null = null,
  ) => {
    const board = options.board();
    if (
      !options.enabled() ||
      !board ||
      event.button !== 0 ||
      !event.isPrimary ||
      (!edge && homeDragFromControl(event.target))
    )
      return;

    cancel();
    const bounds = board.getBoundingClientRect();
    let offset = { x: 0, y: 0 };

    if (catalogue) {
      offset = {
        x: (((bounds.width + 16) / options.layout().columns) * widget.width - 16) / 2,
        y: 32,
      };
    } else if (event.currentTarget instanceof HTMLElement) {
      const origin = event.currentTarget.getBoundingClientRect();
      offset = { x: event.clientX - origin.left, y: event.clientY - origin.top };
      event.currentTarget.focus({ preventScroll: true });
      options.select(widget.instanceId);
    }

    if (event.pointerType === "mouse") event.preventDefault();
    point = { x: event.clientX, y: event.clientY };
    session.value = {
      pointerId: event.pointerId,
      widget: { ...widget, options: { ...widget.options } },
      start: point,
      offset,
      edge,
      startBoardPoint: { x: point.x - bounds.left, y: point.y - bounds.top },
    };
    boardWidth = bounds.width;
  };

  useEventListener(
    window,
    "pointermove",
    (event) => {
      const source = session.value;
      if (!source || event.pointerId !== source.pointerId) return;

      point = { x: event.clientX, y: event.clientY };
      if (!active.value && Math.hypot(point.x - source.start.x, point.y - source.start.y) < 6)
        return;

      if (event.pointerType === "mouse") event.preventDefault();
      updatePosition();
      if (!active.value) {
        active.value = true;
        frame = requestAnimationFrame(scrollFrame);
      }
    },
    { passive: false },
  );

  useEventListener(window, "pointerup", (event) => {
    if (event.pointerId !== session.value?.pointerId) return;

    if (active.value) {
      point = { x: event.clientX, y: event.clientY };
      updatePosition();

      if (preview.value && candidate.value) {
        options.place(candidate.value);
        if (session.value?.edge)
          options.announce(
            `Widget resized to ${candidate.value.width} columns by ${candidate.value.height} rows.`,
          );
      } else options.announce("That placement does not fit. Your layout has not changed.");
    }

    cancel();
  });

  useEventListener(window, "pointercancel", cancel);
  useEventListener(window, "blur", cancel);
  useEventListener(window, "resize", cancel);
  useEventListener(
    window,
    "keydown",
    (event) => {
      if (event.key !== "Escape" || !session.value) return;

      event.preventDefault();
      event.stopImmediatePropagation();
      cancel();
      options.announce("Placement cancelled.");
    },
    { capture: true },
  );

  useResizeObserver(options.board, ([entry]) => {
    if (session.value && entry && Math.abs(entry.contentRect.width - boardWidth) > 1) cancel();
  });

  watch(options.enabled, (enabled) => {
    if (!enabled) cancel();
  });
  watch(options.layout, cancel);
  onUnmounted(cancel);

  const startResize = (widget: HomeWidget, edge: HomeResizeEdge, event: PointerEvent) =>
    start(widget, event, false, edge);

  return { active, candidate, inside, preview, resizing, start, startResize, cancel };
};
