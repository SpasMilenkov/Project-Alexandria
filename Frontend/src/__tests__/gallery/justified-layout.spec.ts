import { describe, expect, it } from "vitest";

import { aspectOf, buildJustifiedRows } from "@/features/gallery/justified-layout";

const layout = (aspects: number[], containerWidth = 1000, targetHeight = 200, gap = 4) =>
  buildJustifiedRows(aspects, (aspect) => aspect, { containerWidth, gap, targetHeight });

const rowWidth = (row: { tiles: { width: number }[] }, gap = 4) =>
  row.tiles.reduce((sum, tile) => sum + tile.width, 0) + gap * (row.tiles.length - 1);

describe("aspectOf", () => {
  it("falls back to square when dimensions are unknown", () => {
    expect(aspectOf(null, null)).toBe(1);
    expect(aspectOf(4000, 0)).toBe(1);
  });

  it("clamps panoramas and tall screenshots", () => {
    expect(aspectOf(12000, 2000)).toBe(3);
    expect(aspectOf(1080, 4000)).toBe(0.5);
    expect(aspectOf(4000, 3000)).toBeCloseTo(4 / 3);
  });
});

describe("buildJustifiedRows", () => {
  it("fills every complete row to the container width", () => {
    const rows = layout([4 / 3, 3 / 2, 1, 4 / 3, 16 / 9, 3 / 4, 4 / 3, 4 / 3, 1, 3 / 2]);

    for (const row of rows.slice(0, -1)) expect(rowWidth(row)).toBeCloseTo(1000, 6);
  });

  it("gives every tile in a row the row height and its own aspect", () => {
    const [row] = layout([2, 1, 2]);
    const ratios = row.tiles.map((tile) => tile.width / row.height);

    [2, 1, 2].forEach((aspect, index) => expect(ratios[index]).toBeCloseTo(aspect, 6));
  });

  it("keeps the last row at the target height without stretching it", () => {
    const rows = layout([4 / 3, 4 / 3, 4 / 3, 4 / 3, 1]);
    const last = rows[rows.length - 1];

    expect(last.height).toBe(200);
    expect(rowWidth(last)).toBeLessThan(1000);
  });

  it("puts a photo wider than a narrow screen on its own row", () => {
    const rows = layout([3, 1], 360, 120);

    expect(rows[0].tiles).toHaveLength(1);
    expect(rows[0].height).toBeCloseTo(120);
    expect(rowWidth(rows[0])).toBeCloseTo(360);
  });

  it("keeps the input order across rows", () => {
    const aspects = [1, 2, 0.75, 1.5, 1, 1, 2, 0.5];
    const rows = layout(aspects, 600, 150);

    expect(rows.flatMap((row) => row.tiles.map((tile) => tile.item))).toEqual(aspects);
  });

  it("returns no rows for an empty month", () => {
    expect(layout([])).toEqual([]);
  });
});
