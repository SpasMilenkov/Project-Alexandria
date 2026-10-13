import { describe, expect, it } from "vitest";

import { HomeWidgetSize } from "@/enums/home-widget";
import { projectHomeLayout } from "@/utils/home-grid";
import { type HomeResizeEdge, homePointerResize, homeResizeByCells } from "@/utils/home-grid-input";
import { cloneHomeLayout, defaultHomeLayout } from "@/utils/home-layout";

describe("Home resize geometry", () => {
  const edges: { edge: HomeResizeEdge; expected: object }[] = [
    { edge: "top", expected: { x: 3, y: 3, width: 6, height: 5 } },
    { edge: "bottom", expected: { x: 3, y: 4, width: 6, height: 3 } },
    { edge: "left", expected: { x: 2, y: 4, width: 7, height: 4 } },
    { edge: "right", expected: { x: 3, y: 4, width: 5, height: 4 } },
    { edge: "top-left", expected: { x: 2, y: 3, width: 7, height: 5 } },
    { edge: "top-right", expected: { x: 3, y: 3, width: 5, height: 5 } },
    { edge: "bottom-left", expected: { x: 2, y: 4, width: 7, height: 3 } },
    { edge: "bottom-right", expected: { x: 3, y: 4, width: 5, height: 3 } },
  ];

  it.each(edges)("anchors the opposite edges when resizing $edge", ({ edge, expected }) => {
    const widget = { ...defaultHomeLayout().widgets[0]!, x: 3, y: 4 };

    expect(homeResizeByCells(widget, 12, edge, -1, -1)).toMatchObject(expected);
    expect(widget).toMatchObject({ x: 3, y: 4, width: 6, height: 4 });
  });

  it("enforces minima and grid limits independently on each axis", () => {
    const widget = { ...defaultHomeLayout().widgets[0]!, x: 3, y: 4 };

    expect(homeResizeByCells(widget, 12, "bottom-right", -100, -100)).toMatchObject({
      x: 3,
      y: 4,
      width: 3,
      height: 2,
    });
    expect(homeResizeByCells(widget, 12, "top-left", -100, -100)).toMatchObject({
      x: 0,
      y: 0,
      width: 9,
      height: 8,
    });
    expect(homeResizeByCells(widget, 12, "bottom-right", 100, 100)).toMatchObject({
      x: 3,
      y: 4,
      width: 9,
      height: 12,
    });
    expect(homeResizeByCells({ ...widget, y: 252 }, 12, "bottom", 0, 100).height).toBe(4);
    expect(homeResizeByCells({ ...widget, y: 20 }, 12, "top", 0, -100)).toMatchObject({
      y: 12,
      height: 12,
    });
  });

  it("keeps a four-column phone rectangle within bounds and supports a compact height", () => {
    const widget = defaultHomeLayout(4).widgets[0]!;

    expect(homeResizeByCells(widget, 4, "bottom-right", -100, -100)).toMatchObject({
      width: 2,
      height: 2,
    });
    expect(homeResizeByCells(widget, 4, "right", 100, 0).width).toBe(4);
  });

  it("snaps pointer movement using the canvas scroll offset and returns a density hint", () => {
    const widget = defaultHomeLayout().widgets[0]!;
    const resized = homePointerResize(
      { left: 100, top: 120, width: 1184 },
      12,
      widget,
      "bottom-right",
      { point: { x: 720, y: 490 }, startBoardPoint: { x: 600, y: 320 } },
    );

    expect(resized).toMatchObject({ x: 0, y: 0, width: 6, height: 5, size: HomeWidgetSize.Medium });
  });

  it("preserves custom heights and projects widths without changing the desktop document", () => {
    const desktop = defaultHomeLayout();
    desktop.widgets[0] = {
      ...desktop.widgets[0]!,
      width: 9,
      height: 2,
      size: HomeWidgetSize.Small,
    };
    desktop.widgets[1] = {
      ...desktop.widgets[1]!,
      width: 3,
      height: 8,
      size: HomeWidgetSize.Medium,
    };
    const original = cloneHomeLayout(desktop);
    const phone = projectHomeLayout(desktop, 4);

    expect(phone.widgets[0]).toMatchObject({ width: 3, height: 2 });
    expect(phone.widgets[1]).toMatchObject({ width: 2, height: 8 });
    expect(desktop).toEqual(original);
  });
});
