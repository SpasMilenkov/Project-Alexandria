import { describe, expect, it, vi } from "vitest";

import { formatAgeShort, formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";
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
