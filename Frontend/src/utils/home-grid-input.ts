import type { HomeLayout, HomeWidget } from "@/types/home";

import {
  HOME_GRID_GAP,
  HOME_GRID_MAX_HEIGHT,
  HOME_GRID_MAX_ROWS,
  HOME_GRID_ROW_HEIGHT,
} from "@/utils/home-grid";
import { homeWidgetDensity, homeWidgetMinimum } from "@/utils/home-widget-sizing";

export type HomeResizeEdge =
  | "top"
  | "right"
  | "bottom"
  | "left"
  | "top-left"
  | "top-right"
  | "bottom-left"
  | "bottom-right";

export const homeDragFromControl = (target: EventTarget | null) =>
  target instanceof Element &&
  Boolean(
    target.closest(
      'button, input, select, textarea, [role="button"], [role="combobox"], [contenteditable="true"]',
    ),
  );

export const homePointerPosition = (
  bounds: Pick<DOMRect, "left" | "top" | "width">,
  columns: HomeLayout["columns"],
  widget: HomeWidget,
  point: { x: number; y: number },
  offset: { x: number; y: number },
) => {
  const columnStep = (bounds.width + HOME_GRID_GAP) / columns;
  const x = Math.round((point.x - bounds.left - offset.x) / columnStep);
  const y = Math.round((point.y - bounds.top - offset.y) / (HOME_GRID_ROW_HEIGHT + HOME_GRID_GAP));

  return {
    ...widget,
    x: Math.max(0, Math.min(columns - widget.width, x)),
    y: Math.max(0, Math.min(HOME_GRID_MAX_ROWS - widget.height, y)),
  };
};

export const homeArrowOffset = (key: string): { x: number; y: number } | null => {
  const offsets: Record<string, { x: number; y: number }> = {
    ArrowLeft: { x: -1, y: 0 },
    ArrowRight: { x: 1, y: 0 },
    ArrowUp: { x: 0, y: -1 },
    ArrowDown: { x: 0, y: 1 },
  };

  return offsets[key] ?? null;
};

export const homeCanvasContains = (board: HTMLElement, point: { x: number; y: number }) => {
  const bounds = board.getBoundingClientRect();
  let left = Math.max(0, bounds.left);
  let right = Math.min(window.innerWidth, bounds.right);
  let top = Math.max(0, bounds.top);
  let bottom = Math.min(window.innerHeight, bounds.bottom);
  let parent = board.parentElement;

  while (parent) {
    const style = getComputedStyle(parent);
    const viewport = parent.getBoundingClientRect();
    if (/auto|scroll|hidden|clip/u.test(style.overflowY)) {
      top = Math.max(top, viewport.top);
      bottom = Math.min(bottom, viewport.bottom);
    }
    if (/auto|scroll|hidden|clip/u.test(style.overflowX)) {
      left = Math.max(left, viewport.left);
      right = Math.min(right, viewport.right);
    }

    parent = parent.parentElement;
  }

  return point.x >= left && point.x <= right && point.y >= top && point.y <= bottom;
};

export const scrollHomeGrid = (board: HTMLElement, point: { x: number; y: number }) => {
  const bounds = board.getBoundingClientRect();
  if (point.x < bounds.left || point.x > bounds.right) return;

  let parent = board.parentElement;

  while (parent) {
    if (
      parent.scrollHeight > parent.clientHeight &&
      /auto|scroll/u.test(getComputedStyle(parent).overflowY)
    ) {
      const viewport = parent.getBoundingClientRect();
      const top = Math.max(0, viewport.top);
      const bottom = Math.min(window.innerHeight, viewport.bottom);
      let delta = 0;

      if (point.y < top + 48) delta = -12;
      else if (point.y > bottom - 48) delta = 12;

      const previous = parent.scrollTop;
      parent.scrollTop += delta;
      if (parent.scrollTop !== previous) return;
    }

    parent = parent.parentElement;
  }

  if (point.y < 48) window.scrollBy(0, -12);
  else if (point.y > window.innerHeight - 48) window.scrollBy(0, 12);
};

const clamp = (value: number, minimum: number, maximum: number) =>
  Math.max(minimum, Math.min(maximum, value));

export const homeResizeByCells = (
  widget: HomeWidget,
  columns: HomeLayout["columns"],
  edge: HomeResizeEdge,
  dx: number,
  dy: number,
): HomeWidget => {
  const minimum = homeWidgetMinimum(widget.type, columns);
  const candidate = { ...widget };
  const right = widget.x + widget.width;
  const bottom = widget.y + widget.height;

  if (edge.includes("left")) {
    candidate.x = clamp(widget.x + dx, 0, Math.max(0, right - minimum.width));
    candidate.width = right - candidate.x;
  } else if (edge.includes("right")) {
    candidate.width = clamp(
      widget.width + dx,
      Math.min(minimum.width, columns - widget.x),
      columns - widget.x,
    );
  }

  if (edge.includes("top")) {
    candidate.y = clamp(
      widget.y + dy,
      Math.max(0, bottom - HOME_GRID_MAX_HEIGHT),
      Math.max(0, bottom - minimum.height),
    );
    candidate.height = bottom - candidate.y;
  } else if (edge.includes("bottom")) {
    candidate.height = clamp(
      widget.height + dy,
      Math.min(minimum.height, HOME_GRID_MAX_ROWS - widget.y),
      Math.min(HOME_GRID_MAX_HEIGHT, HOME_GRID_MAX_ROWS - widget.y),
    );
  }

  if (candidate.width !== widget.width || candidate.height !== widget.height)
    candidate.size = homeWidgetDensity(candidate.width, candidate.height, columns);

  return candidate;
};

export const homePointerResize = (
  bounds: Pick<DOMRect, "left" | "top" | "width">,
  columns: HomeLayout["columns"],
  widget: HomeWidget,
  edge: HomeResizeEdge,
  gesture: { point: { x: number; y: number }; startBoardPoint: { x: number; y: number } },
) => {
  const { point, startBoardPoint } = gesture;
  const dx = Math.round(
    (point.x - bounds.left - startBoardPoint.x) / ((bounds.width + HOME_GRID_GAP) / columns),
  );
  const dy = Math.round(
    (point.y - bounds.top - startBoardPoint.y) / (HOME_GRID_ROW_HEIGHT + HOME_GRID_GAP),
  );

  return homeResizeByCells(widget, columns, edge, dx, dy);
};
