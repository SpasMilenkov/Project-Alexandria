import { describe, expect, it } from "vitest";

import {
  type ItemBox,
  edgeScrollDelta,
  hasMovedBeyond,
  itemsInMarquee,
  nextSelection,
  normalizeRect,
  placeFloatingPanel,
  rectsIntersect,
  sameMembers,
} from "@/features/explorer/drag/geometry";

const item = (id: string, kind: ItemBox["kind"], left: number, top: number): ItemBox => ({
  bottom: top + 40,
  id,
  kind,
  left,
  right: left + 100,
  top,
});

describe("explorer drag geometry", () => {
  it("normalizes a rectangle dragged in any direction", () => {
    expect(normalizeRect({ x: 50, y: 80 }, { x: 10, y: 20 })).toEqual({
      bottom: 80,
      left: 10,
      right: 50,
      top: 20,
    });
  });

  it("treats touching edges as not intersecting", () => {
    const a = { bottom: 10, left: 0, right: 10, top: 0 };

    expect(rectsIntersect(a, { bottom: 20, left: 10, right: 20, top: 0 })).toBe(false);
    expect(rectsIntersect(a, { bottom: 20, left: 9, right: 20, top: 0 })).toBe(true);
  });

  it("finds only the items under the marquee", () => {
    const items = [
      item("a", "file", 0, 0),
      item("b", "file", 200, 0),
      item("c", "directory", 0, 60),
    ];
    const hits = itemsInMarquee(items, { bottom: 70, left: 50, right: 60, top: 5 });

    expect(hits.map((hit) => hit.id)).toEqual(["a", "c"]);
  });

  it("scales to thousands of items within a frame budget", () => {
    const items = Array.from({ length: 5000 }, (_, index) =>
      item(
        `f${index}`,
        index % 2 === 0 ? "file" : "directory",
        (index % 10) * 110,
        Math.floor(index / 10) * 50,
      ),
    );
    const startedAt = performance.now();
    const hits = itemsInMarquee(items, { bottom: 2000, left: 0, right: 600, top: 0 });

    nextSelection({ directories: new Set(), files: new Set() }, hits, "replace");

    expect(hits.length).toBeGreaterThan(0);
    expect(performance.now() - startedAt).toBeLessThan(50);
  });

  it("replaces, adds to, or toggles the selection captured at gesture start", () => {
    const base = { directories: new Set(["d1"]), files: new Set(["f1", "f2"]) };
    const hits = [
      item("f2", "file", 0, 0),
      item("f3", "file", 0, 0),
      item("d2", "directory", 0, 0),
    ];

    const replaced = nextSelection(base, hits, "replace");
    expect([...replaced.files]).toEqual(["f2", "f3"]);
    expect([...replaced.directories]).toEqual(["d2"]);

    const added = nextSelection(base, hits, "add");
    expect([...added.files].sort()).toEqual(["f1", "f2", "f3"]);
    expect([...added.directories].sort()).toEqual(["d1", "d2"]);

    const toggled = nextSelection(base, hits, "toggle");
    expect([...toggled.files].sort()).toEqual(["f1", "f3"]);
    expect([...toggled.directories].sort()).toEqual(["d1", "d2"]);
  });

  it("compares set membership regardless of insertion order", () => {
    expect(sameMembers(new Set(["a", "b"]), new Set(["b", "a"]))).toBe(true);
    expect(sameMembers(new Set(["a"]), new Set(["a", "b"]))).toBe(false);
    expect(sameMembers(new Set(["a", "c"]), new Set(["a", "b"]))).toBe(false);
  });

  it("scrolls faster the closer the pointer gets to an edge", () => {
    const viewport = { bottom: 500, top: 100 };

    expect(edgeScrollDelta(300, viewport)).toBe(0);
    expect(edgeScrollDelta(140, viewport)).toBeLessThan(0);
    expect(edgeScrollDelta(100, viewport)).toBeLessThan(edgeScrollDelta(140, viewport));
    expect(edgeScrollDelta(480, viewport)).toBeGreaterThan(0);
    expect(edgeScrollDelta(560, viewport)).toBe(18);
  });

  it("detects movement beyond a threshold", () => {
    expect(hasMovedBeyond({ x: 0, y: 0 }, { x: 3, y: 0 }, 4)).toBe(false);
    expect(hasMovedBeyond({ x: 0, y: 0 }, { x: 3, y: 3 }, 4)).toBe(true);
  });

  describe("placeFloatingPanel", () => {
    const size = { height: 320, width: 288 };
    const viewport = { height: 900, width: 1400 };

    it("prefers the right side of the anchor", () => {
      const anchor = { bottom: 260, left: 100, right: 260, top: 100 };

      expect(placeFloatingPanel(anchor, size, viewport, { x: 150, y: 150 })).toEqual({
        x: 266,
        y: 100,
      });
    });

    it("flips to the left near the right edge", () => {
      const anchor = { bottom: 260, left: 1200, right: 1360, top: 100 };

      expect(placeFloatingPanel(anchor, size, viewport, { x: 1250, y: 150 }).x).toBe(906);
    });

    it("sits beside the pointer at row height for full-width rows", () => {
      const anchor = { bottom: 344, left: 0, right: 1400, top: 300 };

      expect(placeFloatingPanel(anchor, size, viewport, { x: 700, y: 320 })).toEqual({
        x: 718,
        y: 300,
      });
    });

    it("flips to the pointer's left side and stays above the bottom margin", () => {
      const anchor = { bottom: 880, left: 0, right: 1400, top: 836 };

      expect(placeFloatingPanel(anchor, size, viewport, { x: 1300, y: 850 })).toEqual({
        x: 994,
        y: 572,
      });
    });
  });
});
