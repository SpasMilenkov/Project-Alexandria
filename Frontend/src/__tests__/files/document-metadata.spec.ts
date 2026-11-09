import { describe, expect, it } from "vitest";

import {
  countTiles,
  describeDocumentMoment,
  formatEditingTime,
  formatLanguage,
  formatSeriesPosition,
} from "@/features/files/document-metadata";

describe("describeDocumentMoment", () => {
  it("keeps the author's wall clock when no time zone was recorded", () => {
    const moment = describeDocumentMoment("2026-01-08T23:30:00Z", null);

    expect(moment.date).toContain("2026");
    expect(moment.date).toContain("8");
    expect(moment.zoneNote).toBeUndefined();
  });

  it("shifts a true instant onto the author's own calendar day", () => {
    // 23:30 UTC is already the next morning at UTC+02:00.
    const moment = describeDocumentMoment("2026-01-08T23:30:00Z", 120);

    expect(moment.date).toContain("9");
    expect(moment.zoneNote).toBe("Author's time zone, UTC+02:00");
  });
});

describe("formatEditingTime", () => {
  it("reads minutes, hours, and both", () => {
    expect(formatEditingTime(42)).toBe("42 min");
    expect(formatEditingTime(120)).toBe("2 h");
    expect(formatEditingTime(68)).toBe("1 h 8 min");
  });
});

describe("formatLanguage", () => {
  it("names well-formed tags and passes anything else through", () => {
    expect(formatLanguage("en-US")).not.toBe("en-US");
    expect(formatLanguage("English (UK)")).toBe("English (UK)");
    expect(formatLanguage("x")).toBe("x");
  });
});

describe("countTiles", () => {
  it("adds one tile per recorded count, in reading order", () => {
    const tiles = countTiles({ pageCount: 3, slideCount: null, sheetCount: null, wordCount: 1200 });

    expect(tiles.map((tile) => tile.key)).toEqual(["pages", "words"]);
    expect(tiles[0]?.value).toBe("3");
  });

  it("returns nothing when the file counted nothing", () => {
    expect(
      countTiles({ pageCount: null, slideCount: null, sheetCount: null, wordCount: null }),
    ).toEqual([]);
  });
});

describe("formatSeriesPosition", () => {
  it("reads whole and in-between positions", () => {
    expect(formatSeriesPosition(2)).toBe("Book 2");
    expect(formatSeriesPosition(2.5)).toContain("2");
    expect(formatSeriesPosition(2.5)).toContain("5");
  });
});
