import { describe, expect, it } from "vitest";

import { chunkArray } from "@/utils/chunk";

describe("chunkArray", () => {
  it("splits evenly divisible input", () => {
    expect(chunkArray([1, 2, 3, 4], 2)).toEqual([
      [1, 2],
      [3, 4],
    ]);
  });

  it("keeps a smaller remainder chunk", () => {
    expect(chunkArray([1, 2, 3, 4, 5], 2)).toEqual([[1, 2], [3, 4], [5]]);
  });

  it("returns a single chunk when input fits", () => {
    expect(chunkArray([1, 2], 100)).toEqual([[1, 2]]);
  });

  it("returns empty for empty input", () => {
    expect(chunkArray([], 100)).toEqual([]);
  });

  it("returns empty for non-positive size", () => {
    expect(chunkArray([1, 2], 0)).toEqual([]);
  });
});
