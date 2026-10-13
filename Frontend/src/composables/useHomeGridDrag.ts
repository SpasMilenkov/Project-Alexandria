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
  select: (instanceId: string, silent?: boolean) => void;
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

interface SessionOriginArgs {
  options: GridDragOptions;
  widget: HomeWidget;
  event: PointerEvent;
  board: HTMLElement;
  bounds: DOMRect;
  catalogue: boolean;
}

const resolveSessionOrigin = ({
  options,
  widget,
  event,
  board,
  bounds,
  catalogue,
}: SessionOriginArgs) => {
  if (catalogue) {
    board.focus({ preventScroll: true });

    return {
      offset: {
        x: (((bounds.width + 16) / options.layout().columns) * widget.width - 16) / 2,
        y: 32,
      },
      target: null as HTMLElement | null,
    };
  }

  if (!(event.currentTarget instanceof HTMLElement))
    return { offset: { x: 0, y: 0 }, target: null as HTMLElement | null };

  const card = board.querySelector<HTMLElement>(
    `[data-widget-id="${CSS.escape(widget.instanceId)}"]`,
  );
  const origin = (card ?? event.currentTarget).getBoundingClientRect();

  return {
    offset: { x: event.clientX - origin.left, y: event.clientY - origin.top },
    target: event.currentTarget,
  };
};

export const useHomeGridDrag = (options: GridDragOptions) => {
  const session = shallowRef<PointerSession | null>(null);
  const candidate = shallowRef<HomeWidget | null>(null);
  const active = ref(false);
  const inside = ref(false);
  const suppressClick = ref(false);
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
    inside.value = Boolean(source.edge) || homeCanvasContains(board, point, bounds);
    let next = source.widget;

    if (source.edge) {
      next = homePointerResize(bounds, options.layout().columns, source.widget, source.edge, {
        point,
        startBoardPoint: source.startBoardPoint,
      });
    } else {
      next = homePointerPosition(
        bounds,
        options.layout().columns,
        source.widget,
        point,
        source.offset,
      );
    }

    const current = candidate.value;
    if (
      current?.x === next.x &&
      current.y === next.y &&
      current.width === next.width &&
      current.height === next.height
    )
      return;

    candidate.value = next;
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
    fromToolbar = false,
  ) => {
    const board = options.board();
    if (
      !options.enabled() ||
      !board ||
      event.button !== 0 ||
      !event.isPrimary ||
      (!catalogue && !edge && !fromToolbar && homeDragFromControl(event.target))
    )
      return;

    cancel();
    suppressClick.value = false;

    const bounds = board.getBoundingClientRect();
    const origin = resolveSessionOrigin({ options, widget, event, board, bounds, catalogue });

    if (event.pointerType === "mouse") event.preventDefault();
    point = { x: event.clientX, y: event.clientY };
    session.value = {
      pointerId: event.pointerId,
      widget: { ...widget, options: { ...widget.options } },
      start: point,
      offset: origin.offset,
      edge,
      startBoardPoint: { x: point.x - bounds.left, y: point.y - bounds.top },
    };
    boardWidth = bounds.width;

    if (origin.target) {
      origin.target.focus({ preventScroll: true });
      options.select(widget.instanceId, true);
    }
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

    if (active.value) suppressClick.value = true;

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

  return {
    active,
    candidate,
    inside,
    preview,
    resizing,
    widgetId: computed(() => session.value?.widget.instanceId ?? null),
    start,
    startResize: (widget: HomeWidget, edge: HomeResizeEdge, event: PointerEvent) =>
      start(widget, event, false, edge),
    cancel,
    hasSession: () => session.value !== null,
    consumeSuppressClick: () => {
      const suppressed = suppressClick.value;
      suppressClick.value = false;

      return suppressed;
    },
  };
};
