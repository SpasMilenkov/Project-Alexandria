import { describe, expect, it } from "vitest";

import { lastScheduledSwitch } from "@/features/appearance/color-schedule";

const at = (day: number, hours: number, minutes = 0) => new Date(2026, 10, day, hours, minutes);

describe("lastScheduledSwitch", () => {
  it("is light during the day window", () => {
    const result = lastScheduledSwitch(at(2, 12), "07:00", "19:00");

    expect(result).toEqual({ at: at(2, 7), phase: "light" });
  });

  it("is dark in the evening", () => {
    const result = lastScheduledSwitch(at(2, 21, 30), "07:00", "19:00");

    expect(result).toEqual({ at: at(2, 19), phase: "dark" });
  });

  it("carries the previous evening's switch past midnight", () => {
    const result = lastScheduledSwitch(at(2, 3), "07:00", "19:00");

    expect(result).toEqual({ at: at(1, 19), phase: "dark" });
  });

  it("switches exactly on the boundary minute", () => {
    expect(lastScheduledSwitch(at(2, 7), "07:00", "19:00")?.phase).toBe("light");
    expect(lastScheduledSwitch(at(2, 19), "07:00", "19:00")?.phase).toBe("dark");
  });

  it("supports a light window that spans midnight", () => {
    expect(lastScheduledSwitch(at(2, 23), "22:00", "06:00")?.phase).toBe("light");
    expect(lastScheduledSwitch(at(2, 12), "22:00", "06:00")?.phase).toBe("dark");
  });

  it("returns null for equal or malformed times", () => {
    expect(lastScheduledSwitch(at(2, 12), "07:00", "07:00")).toBeNull();
    expect(lastScheduledSwitch(at(2, 12), "7am", "19:00")).toBeNull();
    expect(lastScheduledSwitch(at(2, 12), "07:00", "24:00")).toBeNull();
  });
});
