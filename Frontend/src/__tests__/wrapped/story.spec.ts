import { describe, expect, it } from "vitest";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import { wrappedFixtures } from "@/utils/wrapped-fixtures";
import {
  calendarMonths,
  replayMarks,
  returnGeometry,
  vinylGroove,
  waveformDays,
} from "@/utils/wrapped-story.utils";

describe("Wrapped story mappings", () => {
  it("counts eighteen individual marks and preserves grouped remainders", () => {
    expect(replayMarks(18).marks).toHaveLength(18);
    expect(replayMarks(18).unit).toBe(1);

    for (const count of [120, 121, 1800, 99999]) {
      const tally = replayMarks(count);

      expect(tally.marks.length).toBeLessThanOrEqual(120);
      expect(tally.marks.reduce((sum, mark) => sum + mark.value, 0)).toBe(count);
    }
  });

  it("keeps the return story on a fixed 720-wide canvas with a bounded gap", () => {
    const short = returnGeometry("2024-01-01", "2024-01-11");
    const long = returnGeometry("2024-01-01", "2025-01-01");

    expect(short.days).toBe(10);
    expect(long.days).toBe(366);
    expect(short.width).toBe(720);
    expect(long.width).toBe(720);
    expect(short.height).toBe(176);
    expect(long.apex.x).toBeGreaterThan(short.apex.x);
    expect(short.apex.x).toBeGreaterThan(32);
    expect(long.apex.x).toBeLessThan(long.width - 32);
    expect(long.labelLeft).toBeGreaterThanOrEqual(0);
    expect(long.labelLeft).toBeLessThanOrEqual(1);
  });

  it("aligns a leap-day streak in Monday-first grids across a month boundary", () => {
    const months = calendarMonths([], "2024-02-01", "2024-03-04", "2024-02-28", "2024-03-02");

    expect(months[0]!.cells[3]!.date).toBe("2024-02-01");
    expect(months[0]!.cells[31]!.date).toBe("2024-02-29");
    expect(months.flatMap((month) => month.cells).filter((cell) => cell?.streak)).toHaveLength(4);

    expect(months[1]!.cells.find((cell) => cell?.date === "2024-03-04")).toMatchObject({
      inRange: false,
      future: true,
    });
  });

  it("keeps a peak window's original daily values and can reveal the whole year", () => {
    const series = Array.from({ length: 366 }, (_, i) => ({
      date: new Date(Date.UTC(2024, 0, i + 1)).toISOString().slice(0, 10),
      seconds: i,
      plays: 1,
    }));

    const focused = waveformDays(series, series[200]!.date, false);

    expect(focused).toHaveLength(31);
    expect(focused[15]).toBe(series[200]);
    expect(waveformDays(series, series[200]!.date, true)).toBe(series);
  });

  it("keeps gallery daily, monthly, summary and endpoint data consistent", () => {
    for (const fixture of wrappedFixtures) {
      const response = fixture.response;

      const hero = response.deck.cards.find(
        (card) => card.type === WrappedCardType.ListeningTime,
      )!.facts!;

      const bookends = response.deck.cards.find(
        (card) => card.type === WrappedCardType.Bookends,
      )!.facts!;

      expect(bookends.series.reduce((sum, day) => sum + day.seconds, 0)).toBe(
        response.summary!.seconds,
      );

      for (const month of hero.series) {
        expect(
          bookends.series
            .filter((day) => day.date.startsWith(month.date.slice(0, 7)))
            .reduce((sum, day) => sum + day.seconds, 0),
        ).toBe(month.seconds);
      }

      const active = bookends.series.filter((day) => day.seconds > 0);

      expect(active).toHaveLength(response.summary!.activeDays);
      expect(bookends.from).toBe(active[0]!.date);
      expect(bookends.to).toBe(active[active.length - 1]!.date);
    }
  });

  it("does not manufacture a full groove for a single session", () => {
    const response = wrappedFixtures.find((fixture) => fixture.id === "sparse")!.response;

    const facts = response.deck.cards.find(
      (card) => card.type === WrappedCardType.Bookends,
    )!.facts!;

    const groove = vinylGroove(facts, response.from, response.to);

    expect(groove.single).toBe(true);
    expect(groove.first).toEqual(groove.last);
    expect(facts.from).toBe("2025-09-01");
  });
});
