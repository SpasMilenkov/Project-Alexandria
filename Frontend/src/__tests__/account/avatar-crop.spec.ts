import { describe, expect, it } from "vitest";

import {
  AVATAR_MAX_ZOOM,
  type CropFrame,
  clampOffsets,
  gifFits,
  panBy,
  sourceRect,
  zoomTo,
} from "@/features/account/avatar-crop";

const LANDSCAPE: CropFrame = { naturalHeight: 1000, naturalWidth: 2000, viewport: 250 };
const CENTERED = { offsetX: 0, offsetY: 0, zoom: 1 };

describe("avatar crop", () => {
  it("crops the centred square of the short side at rest", () => {
    expect(sourceRect(LANDSCAPE, CENTERED)).toEqual({ size: 1000, x: 500, y: 0 });
  });

  it("keeps the image covering the viewport while panning", () => {
    const panned = panBy(LANDSCAPE, CENTERED, 10_000, 10_000);

    // At zoom 1 the image is 500x250 on screen, so it can slide 125px sideways and not at all vertically.
    expect(panned.offsetX).toBe(125);
    expect(panned.offsetY).toBe(0);
    expect(sourceRect(LANDSCAPE, panned).x).toBe(0);
  });

  it("zooms around the viewport centre", () => {
    const panned = { offsetX: 50, offsetY: 0, zoom: 1 };
    const before = sourceRect(LANDSCAPE, panned);
    const after = sourceRect(LANDSCAPE, zoomTo(LANDSCAPE, panned, 2));

    expect(after.size).toBe(before.size / 2);
    expect(after.x + after.size / 2).toBeCloseTo(before.x + before.size / 2);
  });

  it("caps the zoom range", () => {
    expect(zoomTo(LANDSCAPE, CENTERED, 99).zoom).toBe(AVATAR_MAX_ZOOM);
    expect(zoomTo(LANDSCAPE, CENTERED, 0.1).zoom).toBe(1);
  });

  it("re-clamps offsets that no longer fit after zooming out", () => {
    const zoomedIn = clampOffsets(LANDSCAPE, { offsetX: 0, offsetY: 120, zoom: 3 });
    const zoomedOut = zoomTo(LANDSCAPE, zoomedIn, 1);

    expect(zoomedIn.offsetY).toBe(120);
    expect(zoomedOut.offsetY).toBe(0);
  });

  it("matches the server's GIF dimension limit", () => {
    expect(gifFits(1024, 1024)).toBe(true);
    expect(gifFits(1025, 200)).toBe(false);
    expect(gifFits(200, 1025)).toBe(false);
  });
});
