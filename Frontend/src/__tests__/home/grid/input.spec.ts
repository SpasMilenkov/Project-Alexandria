import { describe, expect, it } from "vitest";

import { homeArrowOffset, homeDragFromControl, homePointerPosition } from "@/utils/home-grid-input";
import { defaultHomeLayout } from "@/utils/home-layout";

describe("Home grid input geometry", () => {
  it("preserves the grab offset and snaps a medium clock to the right half", () => {
    const widget = defaultHomeLayout().widgets[0]!;
    const position = homePointerPosition(
      { left: 100, top: 200, width: 1184 },
      12,
      widget,
      { x: 720, y: 390 },
      { x: 20, y: 30 },
    );

    expect(position).toMatchObject({ x: 6, y: 2, width: 6, height: 4 });
    expect(widget).toMatchObject({ x: 0, y: 0 });
  });

  it("clamps a drop to whole cells within the four-column mobile bounds", () => {
    const widget = defaultHomeLayout(4).widgets[0]!;
    const position = homePointerPosition(
      { left: 0, top: 0, width: 390 },
      4,
      widget,
      { x: 1000, y: 999999 },
      { x: 0, y: 0 },
    );

    expect(position).toMatchObject({ x: 0, y: 252, width: 4, height: 4 });
    expect(
      homePointerPosition(
        { left: 0, top: 0, width: 390 },
        4,
        widget,
        { x: -100, y: -100 },
        { x: 0, y: 0 },
      ),
    ).toMatchObject({ x: 0, y: 0 });
  });

  it("maps all four arrow keys and leaves other keys to their normal handlers", () => {
    expect(homeArrowOffset("ArrowLeft")).toEqual({ x: -1, y: 0 });
    expect(homeArrowOffset("ArrowRight")).toEqual({ x: 1, y: 0 });
    expect(homeArrowOffset("ArrowUp")).toEqual({ x: 0, y: -1 });
    expect(homeArrowOffset("ArrowDown")).toEqual({ x: 0, y: 1 });
    expect(homeArrowOffset("Enter")).toBeNull();
  });

  it("exempts controls while allowing headings, content and shortcut links as drag origins", () => {
    const card = document.createElement("section");
    card.innerHTML =
      '<h2>Clock</h2><a href="/dashboard">Files</a><button><span>Retry</span></button><input />';

    expect(homeDragFromControl(card.querySelector("h2"))).toBe(false);
    expect(homeDragFromControl(card.querySelector("a"))).toBe(false);
    expect(homeDragFromControl(card.querySelector("span"))).toBe(true);
    expect(homeDragFromControl(card.querySelector("input"))).toBe(true);
  });
});
