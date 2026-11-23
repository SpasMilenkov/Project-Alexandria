import { describe, expect, it } from "vitest";

import {
  clampTimestamp,
  joinTimestamp,
  lastStart,
  padUnit,
  splitTimestamp,
  timestampUnits,
  unitMax,
} from "@/features/notes/timestamp-fields";

describe("timestampUnits", () => {
  it("adds hours only for recordings an hour or longer, or of unknown length", () => {
    expect(timestampUnits(192)).toEqual(["minutes", "seconds"]);
    expect(timestampUnits(3599)).toEqual(["minutes", "seconds"]);
    expect(timestampUnits(3600)).toEqual(["hours", "minutes", "seconds"]);
    expect(timestampUnits(null)).toEqual(["hours", "minutes", "seconds"]);
  });
});

describe("clampTimestamp", () => {
  it("keeps a start inside the recording, one second before its end", () => {
    expect(lastStart(192)).toBe(191);
    expect(clampTimestamp(500, 192)).toBe(191);
    expect(clampTimestamp(-4, 192)).toBe(0);
    expect(clampTimestamp(90.7, 192)).toBe(90);
  });

  it("caps an unknown length at a day", () => {
    expect(clampTimestamp(100_000, null)).toBe(24 * 3600 - 1);
  });
});

describe("splitTimestamp and joinTimestamp", () => {
  it("round-trips with and without an hours segment", () => {
    expect(splitTimestamp(90, 192)).toEqual({ hours: 0, minutes: 1, seconds: 30 });
    expect(splitTimestamp(3723, 7200)).toEqual({ hours: 1, minutes: 2, seconds: 3 });
    expect(joinTimestamp({ hours: 1, minutes: 2, seconds: 3 })).toBe(3723);
  });
});

describe("unitMax", () => {
  it("lets hours go as far as the recording does", () => {
    expect(unitMax("hours", 7200)).toBe(1);
    expect(unitMax("hours", 9000)).toBe(2);
    expect(unitMax("minutes", 9000)).toBe(59);
  });
});

describe("padUnit", () => {
  it("shows two digits", () => {
    expect(padUnit(5)).toBe("05");
    expect(padUnit(42)).toBe("42");
  });
});
