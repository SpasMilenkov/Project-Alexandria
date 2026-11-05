import { onScopeDispose } from "vue";

import { type Point, edgeScrollDelta } from "@/features/explorer/drag/geometry";

const EDGE_REACH = 48;

/**
 * One animation-frame loop that scrolls a container while the pointer rests near
 * (or just beyond) its top or bottom edge. `onFrame` runs after every scroll step
 * so callers can re-run hit-testing against the moved content in the same frame.
 */
export const useEdgeAutoScroll = (onFrame: () => void) => {
  let frame = 0;
  let container: HTMLElement | null = null;
  let point: Point | null = null;

  const scrollStep = () => {
    if (!container || !point) return;

    const bounds = container.getBoundingClientRect();
    const isBeside = point.x >= bounds.left && point.x <= bounds.right;
    const isNear = point.y >= bounds.top - EDGE_REACH && point.y <= bounds.bottom + EDGE_REACH;

    if (!isBeside || !isNear) return;

    const delta = edgeScrollDelta(point.y, bounds, EDGE_REACH);

    if (delta !== 0) container.scrollTop += delta;
  };

  const tick = () => {
    scrollStep();
    onFrame();
    frame = requestAnimationFrame(tick);
  };

  const update = (nextPoint: Point, nextContainer?: HTMLElement | null) => {
    point = nextPoint;
    if (nextContainer !== undefined) container = nextContainer;
  };

  const start = (startPoint: Point, startContainer: HTMLElement | null) => {
    update(startPoint, startContainer);
    if (frame === 0) frame = requestAnimationFrame(tick);
  };

  const stop = () => {
    if (frame !== 0) cancelAnimationFrame(frame);
    frame = 0;
    container = null;
    point = null;
  };

  onScopeDispose(stop);

  return { start, stop, update };
};
