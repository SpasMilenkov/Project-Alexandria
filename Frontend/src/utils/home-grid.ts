import type { HomeLayout, HomeWidget } from "@/types/home";

import { HomeWidgetSize } from "@/enums/home-widget";
import { homeWidgetMinimum } from "@/utils/home-widget-sizing";

export const HOME_GRID_MAX_ROWS = 256;
export const HOME_GRID_MAX_HEIGHT = 12;
export const HOME_GRID_ROW_HEIGHT = 64;
export const HOME_GRID_GAP = 16;

export type HomeRectangle = Pick<HomeWidget, "x" | "y" | "width" | "height">;

const heights: Record<HomeWidgetSize, number> = {
  [HomeWidgetSize.Small]: 3,
  [HomeWidgetSize.Medium]: 4,
  [HomeWidgetSize.Large]: 6,
};

export const homeWidgetFootprint = (size: HomeWidgetSize, columns: HomeLayout["columns"]) => {
  let width: number = columns;

  if (size === HomeWidgetSize.Small) width = columns === 4 ? 2 : 3;
  else if (size === HomeWidgetSize.Medium && columns === 12) width = 6;

  return { width, height: heights[size] };
};

export const homeSpatialOrder = (first: HomeWidget, second: HomeWidget) =>
  first.y - second.y || first.x - second.x || first.instanceId.localeCompare(second.instanceId);

export const homeRectanglesOverlap = (first: HomeRectangle, second: HomeRectangle) =>
  first.x < second.x + second.width &&
  first.x + first.width > second.x &&
  first.y < second.y + second.height &&
  first.y + first.height > second.y;

export const validHomeRectangle = (rectangle: HomeRectangle, columns: HomeLayout["columns"]) =>
  Object.values(rectangle).every(Number.isInteger) &&
  rectangle.x >= 0 &&
  rectangle.y >= 0 &&
  rectangle.width >= 1 &&
  rectangle.width <= columns &&
  rectangle.height >= 1 &&
  rectangle.height <= HOME_GRID_MAX_HEIGHT &&
  rectangle.x + rectangle.width <= columns &&
  rectangle.y + rectangle.height <= HOME_GRID_MAX_ROWS;

export const firstFreeHomeCell = (
  layout: HomeLayout,
  footprint: Pick<HomeRectangle, "width" | "height">,
): HomeRectangle | null => {
  if (
    !validHomeRectangle(
      { x: 0, y: 0, width: footprint.width, height: footprint.height },
      layout.columns,
    )
  )
    return null;

  for (let y = 0; y <= HOME_GRID_MAX_ROWS - footprint.height; y++) {
    for (let x = 0; x <= layout.columns - footprint.width; x++) {
      const candidate = { x, y, width: footprint.width, height: footprint.height };

      if (!layout.widgets.some((widget) => homeRectanglesOverlap(widget, candidate)))
        return candidate;
    }
  }

  return null;
};

export const previewHomePlacement = (
  layout: HomeLayout,
  candidate: HomeWidget,
): HomeLayout | null => {
  const rectangle = {
    x: candidate.x,
    y: candidate.y,
    width: candidate.width,
    height: candidate.height,
  };
  if (!validHomeRectangle(rectangle, layout.columns)) return null;
  if (
    !layout.widgets.some((widget) => widget.instanceId === candidate.instanceId) &&
    layout.widgets.length >= 32
  )
    return null;

  const placed = [{ ...candidate, options: { ...candidate.options } }];
  const others = layout.widgets
    .filter((widget) => widget.instanceId !== candidate.instanceId)
    .sort(homeSpatialOrder);

  for (const source of others) {
    const widget = { ...source, options: { ...source.options } };
    let collisions = placed.filter((other) => homeRectanglesOverlap(widget, other));

    while (collisions.length) {
      widget.y = Math.max(...collisions.map((other) => other.y + other.height));
      if (widget.y + widget.height > HOME_GRID_MAX_ROWS) return null;

      collisions = placed.filter((other) => homeRectanglesOverlap(widget, other));
    }

    placed.push(widget);
  }

  const byId = new Map(placed.map((widget) => [widget.instanceId, widget]));
  const widgets = layout.widgets.map((widget) => byId.get(widget.instanceId)!);
  if (!layout.widgets.some((widget) => widget.instanceId === candidate.instanceId))
    widgets.push(placed[0]!);

  return { columns: layout.columns, widgets };
};

export const placeHomeWidget = (layout: HomeLayout, candidate: HomeWidget): boolean => {
  const preview = previewHomePlacement(layout, candidate);
  if (!preview) return false;

  layout.widgets = preview.widgets.sort(homeSpatialOrder);

  return true;
};

export const projectHomeLayout = (
  layout: HomeLayout,
  columns: HomeLayout["columns"],
): HomeLayout => {
  const result: HomeLayout = { columns, widgets: [] };

  for (const source of [...layout.widgets].sort(homeSpatialOrder)) {
    const preset = homeWidgetFootprint(source.size, layout.columns);
    let footprint = homeWidgetFootprint(source.size, columns);

    if (source.width !== preset.width || source.height !== preset.height) {
      const minimum = homeWidgetMinimum(source.type, columns);
      footprint = {
        width: Math.max(
          minimum.width,
          Math.min(columns, Math.ceil((source.width * columns) / layout.columns)),
        ),
        height: source.height,
      };
    }
    const position = firstFreeHomeCell(result, footprint);

    if (position) result.widgets.push({ ...source, ...position, options: { ...source.options } });
  }

  return result;
};
