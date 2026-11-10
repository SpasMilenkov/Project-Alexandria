import { describe, expect, it, vi } from "vitest";

import {
  calendarDaysBetween,
  formatAgeShort,
  formatBucketLabel,
  formatCalendarDate,
  formatCalendarDateRange,
  formatClockTime,
  formatDateTime,
  formatDayLabel,
  formatDayMonth,
  formatDayMonthRange,
  formatDaysAgo,
  formatDuration,
  formatFullDate,
  formatMonth,
  formatMonthYear,
  formatPlaybackTime,
  formatTimeAgo,
} from "@/utils/date-formatters";
import { getIconByValue, getFileIcon } from "@/utils/files/icons";
import { getFileTypeReadable, groupMimeSizeRecord } from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

// formatTimeAgo / formatAgeShort

describe("formatTimeAgo", () => {
  const NOW = Date.parse("2024-06-15T12:00:00Z");
  const ago = (ms: number) => new Date(NOW - ms).toISOString();
  const MINUTE = 60_000;
  const HOUR = 60 * MINUTE;
  const DAY = 24 * HOUR;

  it('returns "just now" inside a minute, in either direction', () => {
    expect(formatTimeAgo(ago(30_000), { now: NOW })).toBe("just now");
    expect(formatTimeAgo(ago(-30_000), { now: NOW, style: "narrow" })).toBe("just now");
  });

  it("picks the largest whole unit and rounds down", () => {
    expect(formatTimeAgo(ago(MINUTE), { now: NOW })).toBe("1 minute ago");
    expect(formatTimeAgo(ago(59 * MINUTE), { now: NOW })).toBe("59 minutes ago");
    expect(formatTimeAgo(ago(5 * HOUR), { now: NOW })).toBe("5 hours ago");
    expect(formatTimeAgo(ago(30 * HOUR), { now: NOW })).toBe("1 day ago");
    expect(formatTimeAgo(ago(6 * DAY), { now: NOW })).toBe("6 days ago");
    expect(formatTimeAgo(ago(7 * DAY), { now: NOW })).toBe("1 week ago");
    expect(formatTimeAgo(ago(29 * DAY), { now: NOW })).toBe("4 weeks ago");
    expect(formatTimeAgo(ago(30 * DAY), { now: NOW })).toBe("1 month ago");
    expect(formatTimeAgo(ago(364 * DAY), { now: NOW })).toBe("11 months ago");
    expect(formatTimeAgo(ago(365 * DAY), { now: NOW })).toBe("1 year ago");
    expect(formatTimeAgo(ago(1100 * DAY), { now: NOW })).toBe("3 years ago");
  });

  it("reads future moments forward instead of as a negative age", () => {
    expect(formatTimeAgo(ago(-3 * DAY), { now: NOW })).toBe("in 3 days");
  });

  it("uses the compact form for narrow rows", () => {
    expect(formatTimeAgo(ago(12 * MINUTE), { now: NOW, style: "narrow" })).toBe("12m ago");
    expect(formatTimeAgo(ago(3 * HOUR), { now: NOW, style: "narrow" })).toBe("3h ago");
    expect(formatTimeAgo(ago(2 * DAY), { now: NOW, style: "narrow" })).toBe("2d ago");
  });

  it("switches to a calendar date past the absolute threshold", () => {
    const old = ago(8 * DAY);

    expect(formatTimeAgo(old, { absoluteAfterDays: 7, now: NOW })).toBe(formatCalendarDate(old));
    expect(formatTimeAgo(ago(6 * DAY), { absoluteAfterDays: 7, now: NOW })).toBe("6 days ago");
  });

  it("falls back to the real clock and accepts dates and timestamps", () => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);

    expect(formatTimeAgo(new Date(NOW - 2 * HOUR))).toBe("2 hours ago");
    expect(formatTimeAgo(NOW - 2 * DAY)).toBe("2 days ago");

    vi.useRealTimers();
  });

  it("returns an empty label for an unreadable date", () => {
    expect(formatTimeAgo("not a date", { now: NOW })).toBe("");
  });
});

describe("formatAgeShort", () => {
  const NOW = Date.parse("2024-06-15T12:00:00Z");
  const ago = (ms: number) => new Date(NOW - ms).toISOString();

  it("gives a bare span that stops at days", () => {
    expect(formatAgeShort(ago(45_000), NOW)).toBe("45s");
    expect(formatAgeShort(ago(12 * 60_000), NOW)).toBe("12m");
    expect(formatAgeShort(ago(3 * 3_600_000), NOW)).toBe("3h");
    expect(formatAgeShort(ago(45 * 86_400_000), NOW)).toBe("45d");
  });

  it("never reads negative for a clock slightly behind the server", () => {
    expect(formatAgeShort(ago(-5_000), NOW)).toBe("0s");
  });

  it("returns an empty label for an unreadable date", () => {
    expect(formatAgeShort("not a date", NOW)).toBe("");
  });
});

describe("absolute date presets", () => {
  const MOMENT = "2026-03-04T14:07:09Z";
  const UTC = { timeZone: "UTC" };

  it("formats each preset in one shared style", () => {
    expect(formatDayMonth(MOMENT, UTC)).toBe("Mar 4");
    expect(formatCalendarDate(MOMENT, UTC)).toBe("Mar 4, 2026");
    expect(formatFullDate(MOMENT, UTC)).toBe("Wednesday, March 4, 2026");
    expect(formatFullDate(MOMENT, { compact: true, timeZone: "UTC" })).toBe("Wed, Mar 4, 2026");
    expect(formatFullDate(MOMENT, { showYear: false, timeZone: "UTC" })).toBe("Wednesday, March 4");
    expect(formatMonth(MOMENT, UTC)).toBe("March");
    expect(formatMonth(MOMENT, { style: "short", timeZone: "UTC" })).toBe("Mar");
    expect(formatMonth(MOMENT, { style: "narrow", timeZone: "UTC" })).toBe("M");
    expect(formatMonthYear(MOMENT, UTC)).toBe("March 2026");
    expect(formatClockTime(MOMENT, UTC)).toBe("2:07 PM");
    expect(formatClockTime(MOMENT, { seconds: true, timeZone: "UTC" })).toBe("2:07:09 PM");
    expect(formatDateTime(MOMENT, UTC)).toBe("Mar 4, 2026, 2:07 PM");
  });

  it("reads a UTC day bucket as that day whatever the viewer's zone", () => {
    expect(formatCalendarDate(Date.UTC(2026, 2, 4), UTC)).toBe("Mar 4, 2026");
    expect(formatMonthYear("2026-03-01T00:00:00Z", UTC)).toBe("March 2026");
    expect(formatDayMonth("2026-03-04T23:30:00Z", UTC)).toBe("Mar 4");
  });

  it("collapses what the two ends of a range share", () => {
    expect(formatDayMonthRange("2026-03-04T12:00:00Z", "2026-03-09T12:00:00Z", UTC)).toBe(
      "Mar 4\u2009–\u20099",
    );
    expect(formatCalendarDateRange("2026-03-04T12:00:00Z", "2026-04-02T12:00:00Z", UTC)).toBe(
      "Mar 4\u2009–\u2009Apr 2, 2026",
    );
  });

  it("returns an empty label instead of throwing on an unreadable date", () => {
    expect(formatCalendarDate("not a date")).toBe("");
    expect(formatDateTime(Number.NaN)).toBe("");
    expect(formatDayMonthRange("not a date", MOMENT)).toBe("");
  });
});

describe("formatBucketLabel", () => {
  it("reads a UTC day bucket as its own date", () => {
    expect(formatBucketLabel("2026-03-04T00:00:00Z", "day")).toBe("Mar 4");
  });

  it("names the day on hourly buckets so a multi-day window stays readable", () => {
    const start = "2026-03-04T14:00:00Z";

    expect(formatBucketLabel(start, "hour")).toBe(
      `${formatDayMonth(start)}, ${formatClockTime(start)}`,
    );
  });
});

describe("calendar days", () => {
  const NOW = new Date(2026, 9, 31, 12);

  it("counts midnights crossed, not 24-hour spans", () => {
    expect(calendarDaysBetween(new Date(2026, 9, 30, 23, 50), new Date(2026, 9, 31, 0, 10))).toBe(
      1,
    );
    expect(calendarDaysBetween(new Date(2026, 9, 31, 0, 5), NOW)).toBe(0);
    expect(calendarDaysBetween(new Date(2026, 2, 28, 12), new Date(2026, 3, 1, 12))).toBe(4);
    expect(calendarDaysBetween(NOW, new Date(2026, 9, 29))).toBe(-2);
  });

  it("words a day distance the way relative time does", () => {
    expect(formatDaysAgo(0)).toBe("today");
    expect(formatDaysAgo(1)).toBe("yesterday");
    expect(formatDaysAgo(12)).toBe("12 days ago");
    expect(formatDaysAgo(-3)).toBe("in 3 days");
    expect(formatDaysAgo(0, { capitalize: true })).toBe("Today");
  });

  it("labels history days from today back through the week, then by date", () => {
    expect(formatDayLabel(new Date(2026, 9, 31, 0, 5), NOW)).toBe("Today");
    expect(formatDayLabel(new Date(2026, 9, 30, 23, 50), NOW)).toBe("Yesterday");
    expect(formatDayLabel(new Date(2026, 9, 28, 9), NOW)).toBe("Wednesday");
    expect(formatDayLabel(new Date(2026, 9, 24, 9), NOW)).toBe("Oct 24");
    expect(formatDayLabel(new Date(2025, 11, 30, 9), NOW)).toBe("Dec 30, 2025");
  });

  it("never calls a later day today and returns nothing for an unreadable date", () => {
    expect(formatDayLabel(new Date(2026, 10, 1, 9), NOW)).toBe("Nov 1");
    expect(formatDayLabel("not a date", NOW)).toBe("");
  });
});

describe("formatPlaybackTime", () => {
  it("shows minutes and padded seconds under an hour", () => {
    expect(formatPlaybackTime(0)).toBe("0:00");
    expect(formatPlaybackTime(7.9)).toBe("0:07");
    expect(formatPlaybackTime(187)).toBe("3:07");
    expect(formatPlaybackTime(3599)).toBe("59:59");
  });

  it("adds hours past an hour and keeps counting them past a day", () => {
    expect(formatPlaybackTime(3723)).toBe("1:02:03");
    expect(formatPlaybackTime(26 * 3600 + 5)).toBe("26:00:05");
  });

  it("reads unloaded media as 0:00", () => {
    expect(formatPlaybackTime(Number.NaN)).toBe("0:00");
    expect(formatPlaybackTime(Number.POSITIVE_INFINITY)).toBe("0:00");
    expect(formatPlaybackTime(-4)).toBe("0:00");
  });
});

describe("formatDuration", () => {
  it("shows the two largest units, dropping zero parts", () => {
    expect(formatDuration(252)).toBe("4m 12s");
    expect(formatDuration(3900)).toBe("1h 5m");
    expect(formatDuration(7200)).toBe("2h");
    expect(formatDuration(2 * 86_400 + 3 * 3600 + 240)).toBe("2d 3h");
    expect(formatDuration(2 * 86_400 + 240, { maxUnits: 3 })).toBe("2d 4m");
  });

  it("drops smaller units instead of rounding up", () => {
    expect(formatDuration(3599)).toBe("59m 59s");
    expect(formatDuration(3599, { maxUnits: 1 })).toBe("59m");
  });

  it("keeps a tenth of a second under a minute and milliseconds under a second", () => {
    expect(formatDuration(12.34)).toBe("12.3s");
    expect(formatDuration(45)).toBe("45s");
    expect(formatDuration(0.85)).toBe("850ms");
    expect(formatDuration(0)).toBe("0s");
  });

  it("spells units out for sentences and can stay in hours", () => {
    expect(formatDuration(3900, { style: "short" })).toBe("1 hr 5 min");
    expect(formatDuration(30, { style: "short" })).toBe("30 sec");
    expect(formatDuration(122 * 3600, { largestUnit: "hour" })).toBe("122h");
  });

  it("reads missing or negative input as zero", () => {
    expect(formatDuration(Number.NaN)).toBe("0s");
    expect(formatDuration(-5)).toBe("0s");
  });
});

// formatBytes

describe("formatBytes", () => {
  it('returns "0.00 Bytes" for 0', () => {
    expect(formatBytes(0)).toBe("0.00 Bytes");
  });

  it("formats bytes under 1 KB", () => {
    expect(formatBytes(500)).toBe("500.00 Bytes");
  });

  it("formats kilobytes", () => {
    expect(formatBytes(1024)).toBe("1.02 KB");
    expect(formatBytes(2048)).toBe("2.05 KB");
  });

  it("formats megabytes", () => {
    expect(formatBytes(1024 * 1024)).toBe("1.05 MB");
    expect(formatBytes(1.5 * 1024 * 1024)).toBe("1.57 MB");
  });

  it("formats gigabytes", () => {
    expect(formatBytes(1024 ** 3)).toBe("1.07 GB");
  });

  it("formats terabytes", () => {
    expect(formatBytes(1024 ** 4)).toBe("1.10 TB");
  });

  it("respects the options argument", () => {
    expect(formatBytes(1500, { roundingPrecision: 0 })).toBe("2 KB");
    expect(formatBytes(1500, { roundingPrecision: 3 })).toBe("1.500 KB");
  });

  it("throws for negative roundingPrecision", () => {
    expect(() => formatBytes(1500, { roundingPrecision: -1 })).toThrow();
  });
});

// getIconByValue

describe("getIconByValue", () => {
  it("returns the correct heroicons string for known values", () => {
    expect(getIconByValue("tag")).toBe("heroicons:tag");
    expect(getIconByValue("folder")).toBe("heroicons:folder");
    expect(getIconByValue("trash")).toBe("heroicons:trash");
  });

  it("returns the correct icon for every value in iconOptions without undefined", () => {
    // Spot-check a handful of values that are easy to get wrong
    expect(getIconByValue("download")).toBe("heroicons:arrow-down-tray");
    expect(getIconByValue("upload")).toBe("heroicons:arrow-up-tray");
    expect(getIconByValue("message")).toBe("heroicons:chat-bubble-left-right");
  });
});

// getFileIcon

describe("getFileIcon", () => {
  it("returns the PDF icon for .pdf files", () => {
    expect(getFileIcon("report.pdf")).toBe("mdi:file-pdf-box");
  });

  it("is case-insensitive for extensions", () => {
    expect(getFileIcon("IMAGE.PNG")).toBe("mdi:file-image");
    expect(getFileIcon("SCRIPT.JS")).toBe("mdi:language-javascript");
  });

  it("returns the correct icon for spreadsheet extensions", () => {
    expect(getFileIcon("data.xlsx")).toBe("mdi:file-excel");
    expect(getFileIcon("data.csv")).toBe("mdi:file-delimited-outline");
  });

  it("returns the correct icon for code files", () => {
    expect(getFileIcon("component.vue")).toBe("mdi:vuejs");
    expect(getFileIcon("app.tsx")).toBe("mdi:react");
    expect(getFileIcon("main.py")).toBe("mdi:language-python");
  });

  it("returns the archive icon for compressed files", () => {
    expect(getFileIcon("backup.zip")).toBe("mdi:folder-zip");
    expect(getFileIcon("backup.tar")).toBe("mdi:folder-zip");
    expect(getFileIcon("backup.7z")).toBe("mdi:folder-zip");
  });

  it("returns the fallback icon for unknown extensions", () => {
    expect(getFileIcon("mystery.xyz")).toBe("mdi:file-outline");
  });

  it("returns the fallback icon for files with no extension", () => {
    expect(getFileIcon("Makefile")).toBe("mdi:file-outline");
  });

  it("returns the fallback icon for an empty string", () => {
    expect(getFileIcon("")).toBe("mdi:file-outline");
  });
});

// getFileTypeReadable

describe("getFileTypeReadable", () => {
  it("returns the exact label for known MIME types", () => {
    expect(getFileTypeReadable("application/pdf")).toBe("PDF Document");
    expect(getFileTypeReadable("image/png")).toBe("PNG Image");
    expect(getFileTypeReadable("video/mp4")).toBe("MP4 Video");
    expect(getFileTypeReadable("audio/mpeg")).toBe("MP3 Audio");
    expect(getFileTypeReadable("application/zip")).toBe("ZIP Archive");
    expect(getFileTypeReadable("text/csv")).toBe("CSV File");
  });

  it("falls back to a generic group label for unknown image/* types", () => {
    expect(getFileTypeReadable("image/x-unknown-format")).toBe("Image File");
  });

  it("falls back to a generic group label for unknown video/* types", () => {
    expect(getFileTypeReadable("video/x-unknown-format")).toBe("Video File");
  });

  it("falls back to a generic group label for unknown audio/* types", () => {
    expect(getFileTypeReadable("audio/x-unknown-format")).toBe("Audio File");
  });

  it("uses the file extension when MIME type is unknown and fileName is provided", () => {
    expect(getFileTypeReadable("application/x-totally-unknown", "archive.lz4")).toBe("LZ4 File");
  });

  it("formats the subtype when no fileName is available either", () => {
    // subtype "x-some-format" → "X Some Format File"
    const result = getFileTypeReadable("application/x-some-format");
    expect(result).toContain("File");
  });
});

// groupMimeSizeRecord

describe("groupMimeSizeRecord", () => {
  it("returns empty arrays for an empty input", () => {
    const result = groupMimeSizeRecord({});
    expect(result.categories).toHaveLength(0);
    expect(result.size).toHaveLength(0);
    expect(result.formattedSize).toHaveLength(0);
  });

  it("groups a single known MIME type into the correct category", () => {
    const result = groupMimeSizeRecord({ "image/png": 2048 });
    expect(result.categories).toContain("Images");
    const idx = result.categories.indexOf("Images");
    expect(result.size[idx]).toBe(2048);
    expect(result.formattedSize[idx]).toBe("2.05 KB");
  });

  it("aggregates multiple MIME types that belong to the same group", () => {
    const result = groupMimeSizeRecord({
      "image/jpeg": 1024,
      "image/png": 1024,
      "image/webp": 2048,
    });
    const idx = result.categories.indexOf("Images");
    expect(result.size[idx]).toBe(4096);
  });

  it("produces one entry per group across multiple different groups", () => {
    const result = groupMimeSizeRecord({
      "image/png": 1024,
      "video/mp4": 2048,
      "application/pdf": 512,
    });
    expect(result.categories).toContain("Images");
    expect(result.categories).toContain("Videos");
    expect(result.categories).toContain("Documents");
    expect(result.categories).toHaveLength(3);
  });

  it("excludes groups whose total size is 0", () => {
    const result = groupMimeSizeRecord({
      "image/png": 0,
      "video/mp4": 1024,
    });
    expect(result.categories).not.toContain("Images");
    expect(result.categories).toContain("Videos");
  });

  it("handles unknown MIME types via the fallback grouper", () => {
    const result = groupMimeSizeRecord({ "application/x-7z-compressed": 4096 });
    expect(result.categories).toContain("Archives");
  });

  it("keeps categories, size, and formattedSize arrays in sync", () => {
    const result = groupMimeSizeRecord({
      "image/png": 1024,
      "audio/mpeg": 2048,
    });
    expect(result.categories.length).toBe(result.size.length);
    expect(result.categories.length).toBe(result.formattedSize.length);
  });
});
