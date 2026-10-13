import { describe, expect, it } from "vitest";

import { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import {
  firstFreeHomeCell,
  homeRectanglesOverlap,
  homeWidgetFootprint,
  placeHomeWidget,
  previewHomePlacement,
  projectHomeLayout,
  validHomeRectangle,
} from "@/utils/home-grid";
import { cloneHomeLayout, createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";

describe("Home spatial layout", () => {
  it("keeps a right-side clock and intentional gaps after adding or removing other widgets", () => {
    const layout = defaultHomeLayout();
    layout.widgets[0]!.x = 6;
    const original = cloneHomeLayout(layout);
    const added = createHomeWidget(HomeWidgetType.Clock, "left-clock");
    const position = firstFreeHomeCell(layout, added);

    expect(position).toEqual({ x: 0, y: 0, width: 6, height: 4 });
    expect(placeHomeWidget(layout, { ...added, ...position! })).toBe(true);
    expect(layout.widgets.find((widget) => widget.instanceId === "default-clock")!.x).toBe(6);
    layout.widgets = layout.widgets.filter((widget) => widget.instanceId !== added.instanceId);
    expect(layout).toEqual(original);
  });

  it("previews cascading collisions without changing the draft or horizontal positions", () => {
    const layout = defaultHomeLayout();
    const original = cloneHomeLayout(layout);
    layout.widgets.push({ ...createHomeWidget(HomeWidgetType.Clock, "lower"), x: 6, y: 10 });
    const candidate = { ...layout.widgets[0]!, x: 6, y: 4 };
    const preview = previewHomePlacement(layout, candidate)!;

    expect(preview.widgets.find((widget) => widget.instanceId === "default-library")!.y).toBe(8);
    expect(preview.widgets.find((widget) => widget.instanceId === "lower")).toMatchObject({
      x: 6,
      y: 14,
    });
    expect(layout.widgets[0]).toEqual(original.widgets[0]);
    expect(layout.widgets[1]).toEqual(original.widgets[1]);
    preview.widgets[0]!.options.timeZone = "UTC";
    expect(layout.widgets[0]!.options.timeZone).toBe("local");

    for (const [index, widget] of preview.widgets.entries()) {
      expect(
        preview.widgets.slice(index + 1).some((other) => homeRectanglesOverlap(widget, other)),
      ).toBe(false);
    }
  });

  it("rejects a cascading placement beyond the final row without partially moving widgets", () => {
    const layout = defaultHomeLayout();
    layout.widgets = [
      { ...layout.widgets[0]!, y: 0 },
      { ...layout.widgets[1]!, y: 250 },
    ];
    const original = cloneHomeLayout(layout);

    expect(placeHomeWidget(layout, { ...layout.widgets[0]!, y: 250 })).toBe(false);
    expect(layout).toEqual(original);
  });

  it.each([
    { x: -1, y: 0, width: 6, height: 4 },
    { x: 7, y: 0, width: 6, height: 4 },
    { x: 0, y: -1, width: 6, height: 4 },
    { x: 0, y: 253, width: 6, height: 4 },
    { x: 0.5, y: 0, width: 6, height: 4 },
    { x: 0, y: 0, width: 0, height: 4 },
    { x: 0, y: 0, width: 6, height: 13 },
  ])("rejects invalid rectangle %j", (rectangle) => {
    expect(validHomeRectangle(rectangle, 12)).toBe(false);
  });

  it("allows touching edges and the last in-bounds row", () => {
    expect(validHomeRectangle({ x: 6, y: 252, width: 6, height: 4 }, 12)).toBe(true);
    expect(
      homeRectanglesOverlap(
        { x: 0, y: 0, width: 6, height: 4 },
        { x: 6, y: 0, width: 6, height: 4 },
      ),
    ).toBe(false);
  });

  it("projects mobile footprints in spatial order while leaving desktop coordinates untouched", () => {
    const desktop = defaultHomeLayout();
    desktop.widgets[0]!.x = 6;
    desktop.widgets[0]!.y = 12;
    const original = cloneHomeLayout(desktop);
    const mobile = projectHomeLayout(desktop, 4);

    expect(desktop).toEqual(original);
    expect(mobile.columns).toBe(4);
    expect(mobile.widgets.map((widget) => widget.instanceId)).toEqual([
      "default-library",
      "default-clock",
    ]);
    expect(mobile.widgets[1]).toMatchObject({ width: 4, height: 4, y: 6 });
    mobile.widgets[1]!.options.timeZone = "UTC";
    expect(desktop.widgets[0]!.options.timeZone).toBe("local");
    expect(projectHomeLayout({ columns: 12, widgets: [] }, 4).widgets).toEqual([]);
  });

  it("uses supported size footprints on both device layouts", () => {
    expect(homeWidgetFootprint(HomeWidgetSize.Small, 12)).toEqual({ width: 3, height: 3 });
    expect(homeWidgetFootprint(HomeWidgetSize.Medium, 12)).toEqual({ width: 6, height: 4 });
    expect(homeWidgetFootprint(HomeWidgetSize.Large, 12)).toEqual({ width: 12, height: 6 });
    expect(homeWidgetFootprint(HomeWidgetSize.Small, 4)).toEqual({ width: 2, height: 3 });
    expect(homeWidgetFootprint(HomeWidgetSize.Medium, 4)).toEqual({ width: 4, height: 4 });
  });
});
