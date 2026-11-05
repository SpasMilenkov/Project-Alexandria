import { describe, expect, it } from "vitest";

import type { TimelineDayPoint } from "@/api/stats";

import {
  busiestDay,
  heatLevel,
  heatmapWeeks,
  longestStreak,
  yearBounds,
  yearMonthBars,
} from "@/features/wrapped/listening/year";

const day = (date: string, seconds: number, plays = 1): TimelineDayPoint => ({
  date,
  seconds,
  plays,
  sessionCount: 1,
  top: [],
});

describe("listening year helpers", () => {
  it("bounds a calendar year in UTC", () => {
    expect(yearBounds(2026)).toEqual({
      from: "2026-01-01T00:00:00.000Z",
      to: "2027-01-01T00:00:00.000Z",
    });
  });

  it("lays out Monday-first weeks covering every day of the year once", () => {
    const today = Date.UTC(2026, 9, 9);
    const weeks = heatmapWeeks([day("2026-10-09", 600, 3)], 2026, today);
    const cells = weeks.flatMap((week) => week.cells).filter((cell) => cell !== null);

    expect(cells).toHaveLength(365);
    expect(weeks[0]!.cells.slice(0, 3)).toEqual([null, null, null]);
    expect(weeks[0]!.cells[3]!.date).toBe("2026-01-01");
    expect(weeks.filter((week) => week.month)).toHaveLength(12);
    expect(cells.find((cell) => cell.date === "2026-10-09")).toMatchObject({
      seconds: 600,
      plays: 3,
      future: false,
    });
    expect(cells.find((cell) => cell.date === "2026-10-10")!.future).toBe(true);
  });

  it("buckets intensity into four steps and keeps silent days at zero", () => {
    expect(heatLevel(0, 3600)).toBe(0);
    expect(heatLevel(1, 3600)).toBe(1);
    expect(heatLevel(3600, 3600)).toBe(4);
    expect(heatLevel(900, 0)).toBe(0);
  });

  it("finds the longest run of consecutive listening days", () => {
    const days = [
      day("2026-03-01", 60),
      day("2026-03-02", 60),
      day("2026-03-03", 0),
      day("2026-03-05", 60),
      day("2026-03-06", 60),
      day("2026-03-07", 60),
    ];

    expect(longestStreak(days)).toBe(3);
    expect(longestStreak([])).toBe(0);
  });

  it("picks the day with the most listening", () => {
    expect(busiestDay([day("2026-01-02", 10), day("2026-01-03", 90)])?.date).toBe("2026-01-03");
    expect(busiestDay([day("2026-01-02", 0)])).toBeNull();
  });

  it("scales twelve monthly bars and marks the current and future months", () => {
    const bars = yearMonthBars(
      [
        { date: "2026-02-01", seconds: 1800, plays: 2, sessionCount: 2 },
        { date: "2026-10-01", seconds: 3600, plays: 4, sessionCount: 3 },
      ],
      2026,
      new Date("2026-10-09T12:00:00Z"),
    );

    expect(bars).toHaveLength(12);
    expect(bars[1]!.height).toBe(50);
    expect(bars[9]).toMatchObject({ height: 100, current: true, future: false });
    expect(bars[0]!.height).toBe(0);
    expect(bars[10]!.future).toBe(true);
  });
});
