import { describe, expect, it } from "vitest";

import { HomeRecapPeriod } from "@/enums/home-widget";
import { parseWrappedMonth, wrappedMonth, wrappedMonthRange } from "@/utils/wrapped-month";

describe("Monthly recap periods", () => {
  it("uses UTC months across local offsets and the January rollover", () => {
    const now = new Date("2026-01-01T02:15:00+03:00");

    expect(wrappedMonth(HomeRecapPeriod.CurrentMonth, now)).toBe("2025-12");
    expect(wrappedMonth(HomeRecapPeriod.PreviousMonth, now)).toBe("2025-11");
    expect(wrappedMonth(HomeRecapPeriod.PreviousMonth, new Date("2026-01-01T00:00:00Z"))).toBe(
      "2025-12",
    );
  });

  it("includes every fraction of the last day using the next month's exclusive boundary", () => {
    const now = new Date("2026-10-08T12:00:00Z");

    expect(wrappedMonthRange("2026-09", now)).toEqual({
      from: "2026-09-01T00:00:00.000Z",
      to: "2026-10-01T00:00:00.000Z",
      isFinal: true,
    });
    expect(wrappedMonthRange("2024-02", now).to).toBe("2024-03-01T00:00:00.000Z");
  });

  it("leaves now to the server for current months and marks Final at the exact boundary", () => {
    expect(wrappedMonthRange("2026-09", new Date("2026-09-30T23:59:59.999Z"))).toEqual({
      from: "2026-09-01T00:00:00.000Z",
      to: undefined,
      isFinal: false,
    });
    expect(wrappedMonthRange("2026-09", new Date("2026-10-01T00:00:00Z")).isFinal).toBe(true);
  });

  it.each(["2026-00", "2026-13", "2026-1", "2026-11", "2019-12", "bad", ["2026-09"]])(
    "rejects malformed, unsupported and future month parameters: %s",
    (value) => {
      expect(parseWrappedMonth(value, new Date("2026-10-08T12:00:00Z"))).toBeNull();
    },
  );
});
