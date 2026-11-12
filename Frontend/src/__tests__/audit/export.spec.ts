import { describe, expect, it } from "vitest";

import type { AuditExportPart } from "@/api/activity";

import { AuditExportFormat } from "@/enums/audit-export-format";
import { exportBounds, exportFileName, exportPartSummary } from "@/features/audit/export";
import { formatCount } from "@/utils/count";

const part = (overrides: Partial<AuditExportPart> = {}): AuditExportPart => ({
  endAt: "2026-03-12T08:00:00.123456+00:00",
  endId: "00000000-0000-0000-0000-000000000002",
  index: 1,
  oldestAt: "2026-03-12T09:00:00+00:00",
  rows: 50_000,
  startAt: "2026-11-10T12:00:00.654321+00:00",
  startId: "00000000-0000-0000-0000-000000000001",
  ...overrides,
});

describe("exportBounds", () => {
  it("keeps only the row positions, exactly as the plan sent them", () => {
    expect(exportBounds(part())).toEqual({
      endAt: "2026-03-12T08:00:00.123456+00:00",
      endId: "00000000-0000-0000-0000-000000000002",
      startAt: "2026-11-10T12:00:00.654321+00:00",
      startId: "00000000-0000-0000-0000-000000000001",
    });
  });

  it("leaves the end open on the last part", () => {
    expect(exportBounds(part({ endAt: null, endId: null }))).toMatchObject({
      endAt: null,
      endId: null,
    });
  });
});

describe("exportFileName", () => {
  it("names a single file without a part suffix", () => {
    expect(exportFileName("audit", "20261110-120000", AuditExportFormat.Csv)).toBe(
      "audit-20261110-120000.csv",
    );
    expect(
      exportFileName("audit", "20261110-120000", AuditExportFormat.Json, { count: 1, index: 1 }),
    ).toBe("audit-20261110-120000.json");
  });

  it("numbers the files of a split export", () => {
    expect(
      exportFileName("activity", "20261110-120000", AuditExportFormat.Csv, { count: 3, index: 2 }),
    ).toBe("activity-20261110-120000-part-2-of-3.csv");
  });
});

describe("exportPartSummary", () => {
  it("shows the row count and the span from oldest to newest", () => {
    const summary = exportPartSummary(part());

    expect(summary.startsWith(`${formatCount(50_000, "row")} · `)).toBe(true);
    expect(summary).toContain("2026");
  });
});
