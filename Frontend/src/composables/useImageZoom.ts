import { useElementSize } from "@vueuse/core";
import { type Ref, computed, ref, watch } from "vue";

import {
  MIN_SCALE,
  type Point,
  RESTING,
  type ZoomState,
  clampZoom,
  wheelZoomFactor,
  zoomAround,
} from "@/features/gallery/zoom";

export interface ImageZoomElements {
  // The whole viewing area: it takes the gestures and is what a zoomed photo must cover.
  view: Ref<HTMLElement | null>;
  // The padded box the photo rests centred in; its centre is the photo's transform origin.
  box: Ref<HTMLElement | null>;
  frame: Ref<HTMLElement | null>;
}

export interface ImageZoomHandlers {
  onTurn: (direction: 1 | -1) => void;
  onClose: () => void;
  // Whether a photo exists that way, so a swipe toward one keeps going instead of settling.
  canTurn: (direction: 1 | -1) => boolean;
}

/**
 * How a change reaches the screen: `direct` follows fingers and pinches exactly, `quick`
 * smooths between mouse wheel notches, `smooth` eases taps, buttons, keys and snapping back.
 */
export type ZoomEasing = "direct" | "quick" | "smooth";

const DOUBLE_TAP_SCALE = 2.5;
const DOUBLE_TAP_MS = 300;
const DOUBLE_TAP_SLOP_PX = 30;
// Movement under this is still a tap, since fingers never land perfectly still.
const TAP_SLOP_PX = 8;
const SWIPE_TURN_PX = 60;
const SWIPE_CLOSE_PX = 120;
// A pinch released barely above fit snaps back instead of leaving a few stray pixels of zoom.
const SNAP_BACK_SCALE = 1.05;

const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);
const midpoint = (a: Point, b: Point) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });

/** The scale and pan, kept inside the view, with the eased actions buttons and keys use. */
const useZoomState = ({ view, box, frame }: ImageZoomElements) => {
  const zoom = ref<ZoomState>(RESTING);
  const easing = ref<ZoomEasing>("direct");

  const zoomed = computed(() => zoom.value.scale > MIN_SCALE + 0.001);

  const geometry = () => {
    const viewRect = view.value?.getBoundingClientRect();
    const boxRect = box.value?.getBoundingClientRect();
    if (!viewRect || !boxRect) return null;

    const centre = { x: boxRect.left + boxRect.width / 2, y: boxRect.top + boxRect.height / 2 };

    return {
      centre,
      frame: { height: frame.value?.offsetHeight ?? 0, width: frame.value?.offsetWidth ?? 0 },
      offset: {
        x: centre.x - (viewRect.left + viewRect.width / 2),
        y: centre.y - (viewRect.top + viewRect.height / 2),
      },
      view: { height: viewRect.height, width: viewRect.width },
    };
  };

  /** A screen point relative to the photo's resting centre. */
  const toLocal = (point: Point) => {
    const centre = geometry()?.centre ?? point;

    return { x: point.x - centre.x, y: point.y - centre.y };
  };

  const apply = (next: ZoomState, how: ZoomEasing) => {
    const shape = geometry();

    easing.value = how;
    zoom.value = shape ? clampZoom(next, shape.frame, shape.view, shape.offset) : next;
  };

  const zoomBy = (factor: number) => {
    apply(zoomAround(zoom.value, { x: 0, y: 0 }, zoom.value.scale * factor), "smooth");
  };

  const reset = () => apply(RESTING, "smooth");

  const toggleAt = (point: Point) => {
    if (zoomed.value) {
      reset();
      return;
    }

    apply(zoomAround(zoom.value, toLocal(point), DOUBLE_TAP_SCALE), "smooth");
  };

  const toggle = () => {
    if (zoomed.value) reset();
    else zoomBy(DOUBLE_TAP_SCALE);
  };

  // Panels opening or the window resizing change what the photo has to cover.
  const { width: viewWidth, height: viewHeight } = useElementSize(view);
  const { width: frameWidth, height: frameHeight } = useElementSize(frame);

  watch([viewWidth, viewHeight, frameWidth, frameHeight], () => apply(zoom.value, "direct"));

  const onWheel = (event: WheelEvent) => {
    if (event.deltaY === 0) return;

    event.preventDefault();

    const point = toLocal({ x: event.clientX, y: event.clientY });
    const next = zoomAround(zoom.value, point, zoom.value.scale * wheelZoomFactor(event));

    // Mouse notches ease between steps; a touchpad pinch streams events and tracks directly.
    apply(next, event.ctrlKey ? "direct" : "quick");
  };

  return { apply, easing, onWheel, reset, toLocal, toggle, toggleAt, zoom, zoomBy, zoomed };
};

type ZoomControls = ReturnType<typeof useZoomState>;

/**
 * One pointer tracker for every gesture: pinch, pan once zoomed, double tap, and at fit the
 * swipes that turn the page or close the viewer. Sharing it means a pinch that starts with
 * one finger never also counts as a swipe.
 */
const usePointerGestures = (
  view: Ref<HTMLElement | null>,
  controls: ZoomControls,
  handlers: ImageZoomHandlers,
) => {
  const { apply, easing, reset, toLocal, toggleAt, zoom, zoomed } = controls;

  const swipe = ref<Point>({ x: 0, y: 0 });
  const panning = ref(false);

  const pointers = new Map<number, Point>();
  let gestureStart: Point | null = null;
  let lastPoint: Point | null = null;
  let lastPinch: { spread: number; centre: Point } | null = null;
  let moved = false;
  let pinched = false;
  let lastTap: { point: Point; at: number } | null = null;

  const firstTwo = () => {
    const [a, b] = [...pointers.values()];

    return { centre: midpoint(a, b), spread: distance(a, b) };
  };

  const onPointerDown = (event: PointerEvent) => {
    if (event.pointerType === "mouse" && event.button !== 0) return;

    view.value?.setPointerCapture(event.pointerId);
    const point = { x: event.clientX, y: event.clientY };
    pointers.set(event.pointerId, point);

    if (pointers.size === 2) {
      pinched = true;
      swipe.value = { x: 0, y: 0 };
      lastPinch = firstTwo();
      return;
    }

    if (pointers.size === 1) {
      gestureStart = point;
      lastPoint = point;
      moved = false;
      panning.value = zoomed.value;
    }
  };

  const pinchTo = () => {
    const previous = lastPinch;
    const current = firstTwo();
    if (!previous || previous.spread === 0) return;

    const scaled = zoomAround(
      zoom.value,
      toLocal(current.centre),
      zoom.value.scale * (current.spread / previous.spread),
    );

    apply(
      {
        ...scaled,
        x: scaled.x + current.centre.x - previous.centre.x,
        y: scaled.y + current.centre.y - previous.centre.y,
      },
      "direct",
    );
    lastPinch = current;
  };

  // At fit the photo follows a finger along whichever axis it commits to first: sideways to
  // turn the page, down to close. A mouse drag at fit does nothing.
  const swipeTo = (point: Point, pointerType: string) => {
    if (!gestureStart || pinched || pointerType === "mouse") return;

    const dx = point.x - gestureStart.x;
    const dy = point.y - gestureStart.y;

    swipe.value = Math.abs(dx) >= Math.abs(dy) ? { x: dx, y: 0 } : { x: 0, y: Math.max(0, dy) };
  };

  const panTo = (point: Point) => {
    if (!lastPoint) return;

    const dx = point.x - lastPoint.x;
    const dy = point.y - lastPoint.y;

    apply({ ...zoom.value, x: zoom.value.x + dx, y: zoom.value.y + dy }, "direct");
  };

  const onPointerMove = (event: PointerEvent) => {
    if (!pointers.has(event.pointerId)) return;

    const point = { x: event.clientX, y: event.clientY };
    pointers.set(event.pointerId, point);

    if (pointers.size >= 2) {
      moved = true;
      pinchTo();
      return;
    }

    if (gestureStart && distance(point, gestureStart) > TAP_SLOP_PX) moved = true;

    if (zoomed.value) panTo(point);
    else swipeTo(point, event.pointerType);

    lastPoint = point;
  };

  const settle = () => {
    easing.value = "smooth";
    swipe.value = { x: 0, y: 0 };
  };

  const finishSwipe = () => {
    const { x, y } = swipe.value;
    const direction = x < 0 ? 1 : -1;

    // The photo stays where the finger left it, so the page-turn carries it on out of view
    // instead of it springing back first.
    if (y > SWIPE_CLOSE_PX) {
      handlers.onClose();
      return;
    }

    if (Math.abs(x) > SWIPE_TURN_PX && handlers.canTurn(direction)) {
      handlers.onTurn(direction);
      return;
    }

    settle();
  };

  const registerTap = (point: Point) => {
    const now = performance.now();
    const previous = lastTap;
    const isDouble =
      previous !== null &&
      now - previous.at < DOUBLE_TAP_MS &&
      distance(previous.point, point) < DOUBLE_TAP_SLOP_PX;

    if (isDouble) {
      lastTap = null;
      toggleAt(point);
      return;
    }

    lastTap = { at: now, point };
  };

  const finishGesture = (point: Point, cancelled: boolean) => {
    const wasPinch = pinched;

    pinched = false;
    panning.value = false;
    gestureStart = null;
    lastPoint = null;
    lastPinch = null;

    if (swipe.value.x !== 0 || swipe.value.y !== 0) {
      finishSwipe();
      return;
    }

    if (zoomed.value && zoom.value.scale < SNAP_BACK_SCALE) {
      reset();
      return;
    }

    if (!cancelled && !moved && !wasPinch) registerTap(point);
  };

  const onPointerUp = (event: PointerEvent) => {
    if (!pointers.has(event.pointerId)) return;

    pointers.delete(event.pointerId);

    if (pointers.size === 0) {
      finishGesture({ x: event.clientX, y: event.clientY }, event.type === "pointercancel");
      return;
    }

    // One finger lifted from a pinch: the other carries on as a pan from where it is now.
    lastPinch = null;
    lastPoint = [...pointers.values()][0];
  };

  return { onPointerDown, onPointerMove, onPointerUp, panning, settle, swipe };
};

/** Zoom and pan for one photo in the viewer, with the swipes that work while it sits at fit. */
export const useImageZoom = (elements: ImageZoomElements, handlers: ImageZoomHandlers) => {
  const controls = useZoomState(elements);
  const gestures = usePointerGestures(elements.view, controls, handlers);

  const transform = computed(() => {
    const { scale, x, y } = controls.zoom.value;
    const { x: swipeX, y: swipeY } = gestures.swipe.value;

    return `translate(${x + swipeX}px, ${y + swipeY}px) scale(${scale})`;
  });

  return {
    easing: controls.easing,
    onPointerDown: gestures.onPointerDown,
    onPointerMove: gestures.onPointerMove,
    onPointerUp: gestures.onPointerUp,
    onWheel: controls.onWheel,
    panning: gestures.panning,
    reset: controls.reset,
    settle: gestures.settle,
    toggle: controls.toggle,
    transform,
    zoomBy: controls.zoomBy,
    zoomed: controls.zoomed,
  };
};
