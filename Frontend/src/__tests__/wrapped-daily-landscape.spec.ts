import { describe, expect, it } from "vitest";

import { wrappedDailyLandscape } from "@/utils/wrapped-daily-landscape";

describe("Monthly Wrapped daily skyline", () => {
  it("uses a complete calendar month and one linear height scale", () => {
    const days = wrappedDailyLandscape("2026-10", "2026-11-01T00:00:00Z", [
      { date: "2026-10-03", seconds: 600, plays: 3 },
      { date: "2026-10-09", seconds: 1200, plays: 6 },
    ]);

    expect(days).toHaveLength(31);
    expect(days[0]).toMatchObject({ date: "2026-10-01", seconds: 0, height: 0, future: false });
    expect(days[2]).toMatchObject({ seconds: 600, plays: 3, height: 50 });
    expect(days[8]).toMatchObject({ seconds: 1200, plays: 6, height: 100 });
    expect(days[30]?.future).toBe(false);
  });

  it.each([
    ["2024-02", "2024-03-01T00:00:00Z", 29],
    ["2026-02", "2026-03-01T00:00:00Z", 28],
    ["2026-09", "2026-10-01T00:00:00Z", 30],
    ["2026-12", "2027-01-01T00:00:00Z", 31],
  ])("keeps UTC month lengths for %s", (month, through, count) => {
    const days = wrappedDailyLandscape(month, through, [
      { date: `${month}-01`, seconds: 60, plays: 1 },
    ]);

    expect(days).toHaveLength(count);
    expect(days.every((day) => !day.future)).toBe(true);
  });

  it.each([
    ["2026-10-09T00:00:00Z", 8],
    ["2026-10-09T12:00:00Z", 9],
    ["2026-10-09T00:30:00+03:00", 8],
  ])("uses the exclusive server cutoff %s", (through, observed) => {
    const days = wrappedDailyLandscape("2026-10", through, [
      { date: "2026-10-01", seconds: 60, plays: 1 },
    ]);

    expect(days.filter((day) => !day.future)).toHaveLength(observed);
    expect(days.filter((day) => day.future)).toHaveLength(31 - observed);
  });

  it("excludes future and out-of-month points from values and scaling", () => {
    const days = wrappedDailyLandscape("2026-10", "2026-10-09T12:00:00Z", [
      { date: "2026-09-30", seconds: 5000, plays: 20 },
      { date: "2026-10-09", seconds: 60, plays: 1 },
      { date: "2026-10-10", seconds: 5000, plays: 20 },
      { date: "2026-10-32", seconds: 5000, plays: 20 },
    ]);

    expect(days[8]?.height).toBe(100);
    expect(days[9]).toMatchObject({ future: true, seconds: 0, plays: 0, height: 0 });
  });

  it("preserves quiet days without inventing peaks", () => {
    const days = wrappedDailyLandscape("2026-10", "2026-10-09T12:00:00Z", [
      { date: "2026-10-01", seconds: 0, plays: 0 },
      { date: "2026-10-02", seconds: -5, plays: -1 },
      { date: "2026-10-03", seconds: Number.NaN, plays: Number.NaN },
    ]);

    expect(days).toHaveLength(31);
    expect(days.every((day) => day.seconds === 0 && day.plays === 0 && day.height === 0)).toBe(
      true,
    );
  });

  it("keeps tied listening days at equal height", () => {
    const days = wrappedDailyLandscape("2026-10", "2026-10-09T12:00:00Z", [
      { date: "2026-10-03", seconds: 60, plays: 1 },
      { date: "2026-10-08", seconds: 60, plays: 1 },
    ]);

    expect(days[2]?.height).toBe(100);
    expect(days[7]?.height).toBe(100);
  });

  it("returns no daily chart when the source data or range is unavailable", () => {
    const series = [{ date: "2026-10-01", seconds: 60, plays: 1 }];

    expect(wrappedDailyLandscape("2026-10", "2026-11-01T00:00:00Z", [])).toEqual([]);
    expect(wrappedDailyLandscape("2026-09", "2026-10-01T00:00:00Z", series)).toEqual([]);
    expect(wrappedDailyLandscape("2026-13", "2026-11-01T00:00:00Z", series)).toEqual([]);
    expect(wrappedDailyLandscape("2026-10", "invalid", series)).toEqual([]);
    expect(wrappedDailyLandscape("2026-10", "2026-10-01T00:00:00Z", series)).toEqual([]);
  });
});
