import { clamp } from "@/utils/math";

export interface Point {
  x: number;
  y: number;
}

export interface Box {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

export type ExplorerItemKind = "file" | "directory";

export interface ItemBox extends Box {
  id: string;
  kind: ExplorerItemKind;
}

export interface SelectionSets {
  files: ReadonlySet<string>;
  directories: ReadonlySet<string>;
}

export type MarqueeMode = "replace" | "add" | "toggle";

export interface PanelSize {
  width: number;
  height: number;
}

export interface Viewport {
  width: number;
  height: number;
}

export const normalizeRect = (a: Point, b: Point): Box => ({
  bottom: Math.max(a.y, b.y),
  left: Math.min(a.x, b.x),
  right: Math.max(a.x, b.x),
  top: Math.min(a.y, b.y),
});

export const rectsIntersect = (a: Box, b: Box): boolean =>
  a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;

export const boxContains = (box: Box, point: Point): boolean =>
  point.x >= box.left && point.x <= box.right && point.y >= box.top && point.y <= box.bottom;

export const itemsInMarquee = (items: readonly ItemBox[], marquee: Box): ItemBox[] =>
  items.filter((item) => rectsIntersect(item, marquee));

const toggled = (base: ReadonlySet<string>, hits: readonly string[]) => {
  const next = new Set(base);

  for (const id of hits) {
    if (base.has(id)) next.delete(id);
    else next.add(id);
  }

  return next;
};

/**
 * Selection produced by a marquee over `hits`. Add and toggle modes work against
 * the selection captured when the gesture started, so shrinking the rectangle
 * restores items it previously covered.
 */
export const nextSelection = (
  base: SelectionSets,
  hits: readonly ItemBox[],
  mode: MarqueeMode,
): { files: Set<string>; directories: Set<string> } => {
  const fileHits = hits.filter((hit) => hit.kind === "file").map((hit) => hit.id);
  const directoryHits = hits.filter((hit) => hit.kind === "directory").map((hit) => hit.id);

  if (mode === "replace") {
    return { directories: new Set(directoryHits), files: new Set(fileHits) };
  }

  if (mode === "add") {
    return {
      directories: new Set([...base.directories, ...directoryHits]),
      files: new Set([...base.files, ...fileHits]),
    };
  }

  return {
    directories: toggled(base.directories, directoryHits),
    files: toggled(base.files, fileHits),
  };
};

export const sameMembers = (a: ReadonlySet<string>, b: ReadonlySet<string>): boolean => {
  if (a.size !== b.size) return false;

  for (const id of a) {
    if (!b.has(id)) return false;
  }

  return true;
};

/**
 * Scroll step for a pointer near the top or bottom edge of a scroll viewport.
 * Speed grows linearly as the pointer approaches (or passes) the edge.
 */
export const edgeScrollDelta = (
  pointY: number,
  viewport: { top: number; bottom: number },
  edge = 48,
  maxStep = 18,
): number => {
  const fromTop = pointY - viewport.top;
  const fromBottom = viewport.bottom - pointY;

  if (fromTop < edge) {
    const strength = Math.min(1, (edge - fromTop) / edge);

    return -Math.ceil(strength * maxStep);
  }

  if (fromBottom < edge) {
    const strength = Math.min(1, (edge - fromBottom) / edge);

    return Math.ceil(strength * maxStep);
  }

  return 0;
};

export const hasMovedBeyond = (start: Point, point: Point, threshold: number): boolean =>
  Math.hypot(point.x - start.x, point.y - start.y) > threshold;

export const expandBox = (box: Box, amount: number): Box => ({
  bottom: box.bottom + amount,
  left: box.left - amount,
  right: box.right + amount,
  top: box.top - amount,
});

/**
 * Places a floating panel beside an anchor: right first, then left. Anchors
 * spanning the width (list rows) get the panel beside the pointer instead, at
 * row height, so the rows below stay uncovered. Always stays inside the
 * viewport margin.
 */
export const placeFloatingPanel = (
  anchor: Box,
  size: PanelSize,
  viewport: Viewport,
  pointer: Point,
  spacing: { gap: number; margin: number } = { gap: 6, margin: 8 },
): Point => {
  const { gap, margin } = spacing;
  const maxTop = viewport.height - size.height - margin;
  const sideTop = clamp(anchor.top, margin, maxTop);

  if (anchor.right + gap + size.width <= viewport.width - margin) {
    return { x: anchor.right + gap, y: sideTop };
  }

  if (anchor.left - gap - size.width >= margin) {
    return { x: anchor.left - gap - size.width, y: sideTop };
  }

  const pointerGap = gap * 3;

  if (pointer.x + pointerGap + size.width <= viewport.width - margin) {
    return { x: pointer.x + pointerGap, y: sideTop };
  }

  return {
    x: clamp(pointer.x - pointerGap - size.width, margin, viewport.width - size.width - margin),
    y: sideTop,
  };
};
