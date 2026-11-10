import { describe, expect, it } from "vitest";

import { formatCount, formatNumber, pluralize } from "@/utils/count";

describe("count labels", () => {
  it("uses the singular only for exactly one", () => {
    expect(formatCount(1, "file")).toBe("1 file");
    expect(formatCount(0, "file")).toBe("0 files");
    expect(formatCount(3, "file")).toBe("3 files");
    expect(formatCount(1.5, "file")).toBe("1.5 files");
  });

  it("takes an explicit plural for irregular nouns", () => {
    expect(formatCount(2, "directory", "directories")).toBe("2 directories");
    expect(formatCount(1, "entry", "entries")).toBe("1 entry");
  });

  it("groups large counts the way the locale writes numbers", () => {
    expect(formatCount(12_345, "play")).toBe("12,345 plays");
    expect(formatNumber(1_234_567)).toBe("1,234,567");
  });

  it("gives the noun alone for templates that style the number separately", () => {
    expect(pluralize(1, "collection")).toBe("collection");
    expect(pluralize(4, "collection")).toBe("collections");
    expect(pluralize(2, "retry", "retries")).toBe("retries");
  });
});
