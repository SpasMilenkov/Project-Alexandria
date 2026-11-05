import { describe, expect, it } from "vitest";

import type { DailyStatusPoint, PublicServiceState } from "@/api/publicStatus";

import {
  describeLastIssue,
  fleetWindowStats,
  formatStatusDay,
  serviceWindowStats,
} from "@/features/monitoring/public-status";

const daysOf = (...statuses: PublicServiceState[]): DailyStatusPoint[] =>
  statuses.map((status, index) => ({
    date: `2026-11-${String(index + 1).padStart(2, "0")}`,
    status,
  }));

describe("serviceWindowStats", () => {
  it("reports a clean window as fully operational with no last issue", () => {
    const stats = serviceWindowStats(daysOf("Healthy", "Healthy", "Healthy"));

    expect(stats).toEqual({ daysSinceIssue: null, issueDays: 0, operationalPercent: 100 });
  });

  it("counts degraded and down days as issues and measures from the newest one", () => {
    const stats = serviceWindowStats(daysOf("Down", "Healthy", "Degraded", "Healthy", "Healthy"));

    expect(stats.issueDays).toBe(2);
    expect(stats.daysSinceIssue).toBe(2);
    expect(stats.operationalPercent).toBe(60);
  });

  it("never rounds a window with an issue up to 100%", () => {
    const statuses: PublicServiceState[] = Array.from({ length: 999 }, () => "Healthy");
    statuses.push("Degraded");

    expect(serviceWindowStats(daysOf(...statuses)).operationalPercent).toBe(99.9);
  });
});

describe("fleetWindowStats", () => {
  it("treats a day as clean only when every service was healthy", () => {
    const stats = fleetWindowStats([
      daysOf("Healthy", "Degraded", "Healthy", "Healthy"),
      daysOf("Healthy", "Healthy", "Down", "Healthy"),
    ]);

    expect(stats).toEqual({ cleanDays: 2, daysSinceDisruption: 1, totalDays: 4 });
  });

  it("handles an empty fleet", () => {
    expect(fleetWindowStats([])).toEqual({ cleanDays: 0, daysSinceDisruption: null, totalDays: 0 });
  });
});

describe("describeLastIssue", () => {
  it("phrases the gap relative to today", () => {
    expect(describeLastIssue(null, 90)).toBe("No issues in 90 days");
    expect(describeLastIssue(0, 90)).toBe("Issue today");
    expect(describeLastIssue(1, 90)).toBe("Last issue yesterday");
    expect(describeLastIssue(12, 90)).toBe("Last issue 12 days ago");
  });
});

describe("formatStatusDay", () => {
  it("keeps the UTC calendar date regardless of the local zone", () => {
    const formatted = formatStatusDay("2026-11-01");

    expect(formatted).toContain("Nov");
    expect(formatted).not.toContain("Oct");
  });
});
