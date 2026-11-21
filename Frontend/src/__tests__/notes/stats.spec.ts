import { describe, expect, it } from "vitest";

import { countWords, readingMinutes } from "@/features/notes/stats";

describe("countWords", () => {
  it("counts words and ignores markdown syntax", () => {
    expect(countWords("# Trip to Paris\n\n- **Day one**: the river\n> quote")).toBe(8);
  });

  it("keeps contractions and hyphenated words together", () => {
    expect(countWords("Don't miss the well-known café")).toBe(5);
  });

  it("leaves link and image targets out", () => {
    expect(countWords("[the hotel](https://example.com/a-b) ![photo](sunset.jpg)")).toBe(3);
    expect(countWords("See <https://example.com/page> now")).toBe(2);
  });

  it("is zero for an empty note", () => {
    expect(countWords("")).toBe(0);
  });
});

describe("readingMinutes", () => {
  it("is zero only when there are no words", () => {
    expect(readingMinutes(0)).toBe(0);
    expect(readingMinutes(12)).toBe(1);
  });

  it("rounds to whole minutes", () => {
    expect(readingMinutes(1240)).toBe(5);
    expect(readingMinutes(1400)).toBe(6);
  });
});
