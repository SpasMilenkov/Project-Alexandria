import { describe, expect, it } from "vitest";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import {
  type TimelineMetrics,
  buildTimeline,
  estimateMonthHeight,
  monthDots,
  rowSpanOf,
  sectionAt,
  sectionsBetween,
  yearMarks,
} from "@/features/gallery/timeline";

const metrics: TimelineMetrics = {
  leadingYearHeaderHeight: 64,
  yearHeaderHeight: 104,
  containerWidth: 1000,
  gap: 4,
  headerHeight: 48,
  targetHeight: 200,
};

const photo = (index: number, width = 4000, height = 3000): GalleryPhoto => ({
  fileId: `file-${index}`,
  height,
  mimeType: "image/jpeg",
  name: `IMG_${index}.jpg`,
  takenAt: "2019-06-12T18:15:42.000Z",
  uploadedAt: "2026-11-08T10:00:00.000Z",
  versionId: `version-${index}`,
  width,
});

const months: GalleryMonth[] = [
  { count: 12, month: 3, year: 2024 },
  { count: 30, month: 6, year: 2019 },
  { count: 5, month: 1, year: 2019 },
  { count: 8, month: 9, year: 2018 },
];

describe("buildTimeline", () => {
  it("gives unloaded months a header and one placeholder sized from their count", () => {
    const layout = buildTimeline(months, new Map(), metrics);
    const june = layout.sections[1];

    expect(layout.items.filter((item) => item.kind === "header")).toHaveLength(4);
    expect(layout.items.filter((item) => item.kind === "placeholder")).toHaveLength(4);
    // June is 2019's newest month, so its section also holds the 2019 year header.
    expect(june.end - june.start).toBe(104 + 48 + estimateMonthHeight(30, metrics));
  });

  it("swaps a loaded month's placeholder for justified rows", () => {
    const loaded = new Map([["2019-06", Array.from({ length: 30 }, (_, i) => photo(i))]]);

    const layout = buildTimeline(months, loaded, metrics);
    const rows = layout.items.filter((item) => item.kind === "row");

    expect(rows.every((row) => row.monthKey === "2019-06")).toBe(true);
    expect(rows.flatMap((row) => (row.kind === "row" ? row.row.tiles : []))).toHaveLength(30);
    expect(
      layout.items.some((item) => item.kind === "placeholder" && item.monthKey === "2019-06"),
    ).toBe(false);
  });

  it("lays sections end to end with item ids unique across the timeline", () => {
    const loaded = new Map([["2024-03", Array.from({ length: 12 }, (_, i) => photo(i))]]);

    const layout = buildTimeline(months, loaded, metrics);

    layout.sections.slice(1).forEach((section, index) => {
      expect(section.start).toBe(layout.sections[index].end);
    });
    expect(layout.totalHeight).toBe(layout.sections[layout.sections.length - 1].end);
    expect(layout.totalHeight).toBe(layout.items.reduce((sum, item) => sum + item.size, 0));
    expect(new Set(layout.items.map((item) => item.id)).size).toBe(layout.items.length);
  });
});

describe("year headers", () => {
  it("opens each year once with the year's total, the first without the separating space", () => {
    const layout = buildTimeline(months, new Map(), metrics);
    const years = layout.items.filter((item) => item.kind === "year");

    expect(years.map((item) => [item.year, item.count, item.size])).toEqual([
      [2024, 12, 64],
      [2019, 35, 104],
      [2018, 8, 104],
    ]);
  });

  it("names months alone under the year but keeps the full label for the scrubber", () => {
    const layout = buildTimeline(months, new Map(), metrics);
    const june = layout.items.find((item) => item.kind === "header" && item.monthKey === "2019-06");

    expect(june?.kind === "header" && june.label).not.toMatch(/2019/u);
    expect(layout.sections[1].label).toMatch(/2019/u);
  });
});

describe("estimateMonthHeight", () => {
  it("runs the real layout, so a month of 4:3 photos is estimated exactly", () => {
    const real = buildTimeline(
      [{ count: 40, month: 6, year: 2019 }],
      new Map([["2019-06", Array.from({ length: 40 }, (_, i) => photo(i))]]),
      metrics,
    );
    const estimated =
      estimateMonthHeight(40, metrics) + metrics.headerHeight + metrics.leadingYearHeaderHeight;

    expect(real.totalHeight).toBeCloseTo(estimated, 6);
  });
});

describe("section lookups", () => {
  const layout = buildTimeline(months, new Map(), metrics);

  it("finds the section containing an offset and clamps outside the timeline", () => {
    const june = layout.sections[1];

    expect(sectionAt(layout.sections, june.start)?.key).toBe("2019-06");
    expect(sectionAt(layout.sections, june.end - 1)?.key).toBe("2019-06");
    expect(sectionAt(layout.sections, -50)?.key).toBe("2024-03");
    expect(sectionAt(layout.sections, layout.totalHeight + 50)?.key).toBe("2018-09");
    expect(sectionAt([], 0)).toBeUndefined();
  });

  it("lists every section overlapping a pixel range", () => {
    const june = layout.sections[1];

    expect(
      sectionsBetween(layout.sections, june.start + 1, june.end - 1).map((s) => s.key),
    ).toEqual(["2019-06"]);
    expect(sectionsBetween(layout.sections, june.end - 1, june.end + 1).map((s) => s.key)).toEqual([
      "2019-06",
      "2019-01",
    ]);
  });
});

describe("monthDots", () => {
  it("places one dot per month at the fraction a seek to it uses", () => {
    const layout = buildTimeline(months, new Map(), metrics);

    const dots = monthDots(layout, layout.totalHeight, 1000, 0);

    expect(dots).toEqual(layout.sections.map((section) => section.start / layout.totalHeight));
  });

  it("drops dots that would touch the previous one", () => {
    const layout = buildTimeline(
      Array.from({ length: 120 }, (_, i) => ({
        count: 10,
        month: (i % 12) + 1,
        year: 2020 - Math.floor(i / 12),
      })),
      new Map(),
      metrics,
    );
    const px = monthDots(layout, layout.totalHeight, 300, 6).map((fraction) => fraction * 300);

    expect(px.length).toBeLessThan(120);
    px.slice(1).forEach((value, index) => expect(value - px[index]).toBeGreaterThanOrEqual(6));
  });
});

describe("yearMarks", () => {
  it("marks each year once, at its newest month", () => {
    const layout = buildTimeline(months, new Map(), metrics);

    const marks = yearMarks(layout, layout.totalHeight, 1000, 0);

    expect(marks.map((mark) => mark.year)).toEqual([2024, 2019, 2018]);
    expect(marks[1].fraction).toBeCloseTo(layout.sections[1].start / layout.totalHeight);
  });

  it("drops years that would crowd the previous label", () => {
    const layout = buildTimeline(
      Array.from({ length: 30 }, (_, i) => ({ count: 10, month: 6, year: 2020 - i })),
      new Map(),
      metrics,
    );

    const marks = yearMarks(layout, layout.totalHeight, 200, 22);
    const positions = marks.map((mark) => mark.fraction * 200);

    expect(marks.length).toBeLessThan(30);
    positions
      .slice(1)
      .forEach((px, index) => expect(px - positions[index]).toBeGreaterThanOrEqual(22));
  });

  it("returns nothing before the track has a size", () => {
    expect(yearMarks(buildTimeline(months, new Map(), metrics), 1000, 0, 22)).toEqual([]);
  });
});

describe("rowSpanOf", () => {
  it("returns the pixel span of the row holding a photo", () => {
    const loaded = new Map([["2024-03", Array.from({ length: 12 }, (_, i) => photo(i))]]);
    const layout = buildTimeline(months, loaded, metrics);
    const rows = layout.items.filter((item) => item.kind === "row");
    const second = rows[1];
    const secondStart = layout.items
      .slice(0, layout.items.indexOf(second))
      .reduce((sum, item) => sum + item.size, 0);
    const fileId = second.kind === "row" ? second.row.tiles[0].item.fileId : "";

    expect(rowSpanOf(layout, fileId)).toEqual({
      end: secondStart + second.size,
      start: secondStart,
    });
  });

  it("returns null for a photo that is not laid out", () => {
    const layout = buildTimeline(months, new Map(), metrics);

    expect(rowSpanOf(layout, "file-0")).toBeNull();
  });
});
