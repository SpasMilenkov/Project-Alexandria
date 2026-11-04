import { describe, expect, it } from "vitest";

import {
  MIN_WRAPPED_YEAR,
  padRank,
  parseWrappedYear,
  wrappedEmptyState,
  yearRange,
} from "@/utils/wrapped-display.utils";

describe("parseWrappedYear", () => {
  it("accepts sane years and falls back otherwise", () => {
    expect(parseWrappedYear("2024", 2026)).toBe(2024);
    expect(parseWrappedYear("nope", 2026)).toBe(2026);
    expect(parseWrappedYear("1999", 2026)).toBe(2026);
    expect(parseWrappedYear(undefined, 2026)).toBe(2026);
    expect(parseWrappedYear("2019", 2026)).toBe(2026);
    expect(parseWrappedYear("2020", 2026)).toBe(MIN_WRAPPED_YEAR);
  });
});

describe("yearRange", () => {
  it("uses an exclusive next-year boundary", () => {
    const now = new Date(Date.UTC(2026, 5, 15, 12, 0, 0));
    const range = yearRange(2024, now);

    expect(range.from).toBe("2024-01-01T00:00:00.000Z");
    expect(range.to).toBe("2025-01-01T00:00:00.000Z");
  });

  it("clamps the current year to now", () => {
    const now = new Date(Date.UTC(2026, 5, 15, 12, 0, 0));
    const range = yearRange(2026, now);

    expect(range.from).toBe("2026-01-01T00:00:00.000Z");
    expect(range.to).toBe(now.toISOString());
  });
});

describe("empty years", () => {
  it("explains that new listening cannot fill a past year's history", () => {
    const state = wrappedEmptyState(2025, 2026);

    expect(state.title).toBe("No recorded listening in 2025");
    expect(state.description).toContain("2026 Wrapped, not 2025");
    expect(state.actionLabel).toBe("View 2026 Wrapped");
  });

  it("invites listening only for the current year", () => {
    expect(wrappedEmptyState(2026, 2026).actionLabel).toBe("Find your soundtrack");
    expect(wrappedEmptyState(2026, 2026).description).toContain("Play some music");
  });
});

describe("padRank", () => {
  it("zero-pads single digits", () => {
    expect(padRank(1)).toBe("01");
    expect(padRank(12)).toBe("12");
  });
});
