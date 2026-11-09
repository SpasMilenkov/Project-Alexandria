import { describe, expect, it } from "vitest";

import {
  MAX_SCALE,
  RESTING,
  clampZoom,
  wheelZoomFactor,
  zoomAround,
} from "@/features/gallery/zoom";

const frame = { height: 600, width: 800 };
const view = { height: 800, width: 1200 };
const centred = { x: 0, y: 0 };

describe("zoomAround", () => {
  it("keeps the content under the point in place", () => {
    const point = { x: 200, y: -100 };
    const next = zoomAround(RESTING, point, 2);

    // The content that sat under the point before is (point - pan) / scale from the centre.
    const before = {
      x: (point.x - RESTING.x) / RESTING.scale,
      y: (point.y - RESTING.y) / RESTING.scale,
    };
    const after = { x: (point.x - next.x) / next.scale, y: (point.y - next.y) / next.scale };

    expect(after.x).toBeCloseTo(before.x);
    expect(after.y).toBeCloseTo(before.y);
  });

  it("limits the scale to its range", () => {
    expect(zoomAround(RESTING, centred, 100).scale).toBe(MAX_SCALE);
    expect(zoomAround(RESTING, centred, 0.2).scale).toBe(1);
  });
});

describe("clampZoom", () => {
  it("holds a photo still on an axis where it is smaller than the view", () => {
    // 800x600 at 1.5x is 1200x900: exactly as wide as the view, taller than it.
    const clamped = clampZoom({ scale: 1.5, x: 300, y: 300 }, frame, view, centred);

    expect(clamped.x).toBe(0);
    expect(clamped.y).toBe(50);
  });

  it("lets a zoomed photo pan until its edge meets the view's edge", () => {
    // 3200x2400 inside 1200x800 leaves 1000 sideways and 800 vertically on each side.
    const clamped = clampZoom({ scale: 4, x: -5000, y: 5000 }, frame, view, centred);

    expect(clamped).toEqual({ scale: 4, x: -1000, y: 800 });
  });

  it("accounts for a resting centre that sits off the view's centre", () => {
    const clamped = clampZoom({ scale: 4, x: 0, y: 5000 }, frame, view, { x: 0, y: 24 });

    expect(clamped.y).toBe(800 - 24);
  });

  it("always allows the resting position, so growing past the view never jumps", () => {
    const clamped = clampZoom({ scale: 1.34, x: 0, y: 0 }, frame, view, { x: 0, y: 24 });

    expect(clamped).toEqual({ scale: 1.34, x: 0, y: 0 });
  });
});

describe("wheelZoomFactor", () => {
  it("zooms in on an upward wheel and out on a downward one", () => {
    expect(wheelZoomFactor({ ctrlKey: false, deltaMode: 0, deltaY: -100 })).toBeGreaterThan(1);
    expect(wheelZoomFactor({ ctrlKey: false, deltaMode: 0, deltaY: 100 })).toBeLessThan(1);
  });

  it("treats line-based wheels like their pixel equivalent", () => {
    const lines = wheelZoomFactor({ ctrlKey: false, deltaMode: 1, deltaY: 3 });
    const pixels = wheelZoomFactor({ ctrlKey: false, deltaMode: 0, deltaY: 48 });

    expect(lines).toBeCloseTo(pixels);
  });

  it("makes a small touchpad pinch step count for more than a wheel step of the same size", () => {
    const pinch = wheelZoomFactor({ ctrlKey: true, deltaMode: 0, deltaY: -10 });
    const wheel = wheelZoomFactor({ ctrlKey: false, deltaMode: 0, deltaY: -10 });

    expect(pinch).toBeGreaterThan(wheel);
  });
});
