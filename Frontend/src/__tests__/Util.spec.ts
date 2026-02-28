import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

import { formatDate } from "@/utils/date-formatters";
import { getIconByValue, getFileIcon } from "@/utils/icon.utils";
import { getFileTypeReadable, groupMimeSizeRecord } from "@/utils/mimetype.utils";
import { formatBytes } from "@/utils/size.utils";

// ─── formatDate ───────────────────────────────────────────────────────────────

describe("formatDate", () => {
  // Pin "now" so tests don't drift depending on when they run
  const FAKE_NOW = new Date("2024-06-15T12:00:00Z");

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(FAKE_NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns "Today" for a date earlier the same day', () => {
    expect(formatDate("2024-06-15T08:00:00Z")).toBe("Today");
  });

  it('returns "Yesterday" for exactly 1 day ago', () => {
    expect(formatDate("2024-06-14T12:00:00Z")).toBe("Yesterday");
  });

  it("returns days ago for 2–6 days ago", () => {
    expect(formatDate("2024-06-12T12:00:00Z")).toBe("3 days ago");
    expect(formatDate("2024-06-09T12:00:00Z")).toBe("6 days ago");
  });

  it("returns weeks ago for 7–29 days ago", () => {
    expect(formatDate("2024-06-08T12:00:00Z")).toBe("1 week ago");
    expect(formatDate("2024-05-25T12:00:00Z")).toBe("3 weeks ago");
  });

  it("returns months ago for 30–364 days ago", () => {
    expect(formatDate("2024-05-15T12:00:00Z")).toBe("1 month ago");
    expect(formatDate("2023-12-15T12:00:00Z")).toBe("6 months ago");
  });

  it("returns years ago for 365+ days ago", () => {
    expect(formatDate("2023-06-15T12:00:00Z")).toBe("1 year ago");
    expect(formatDate("2021-06-15T12:00:00Z")).toBe("3 years ago");
  });
});

// ─── formatBytes ─────────────────────────────────────────────────────────────

describe("formatBytes", () => {
  it('returns "0 Bytes" for 0', () => {
    expect(formatBytes(0)).toBe("0 Bytes");
  });

  it("formats bytes under 1 KB", () => {
    expect(formatBytes(500)).toBe("500 Bytes");
  });

  it("formats kilobytes", () => {
    expect(formatBytes(1024)).toBe("1 KB");
    expect(formatBytes(2048)).toBe("2 KB");
  });

  it("formats megabytes", () => {
    expect(formatBytes(1024 * 1024)).toBe("1 MB");
    expect(formatBytes(1.5 * 1024 * 1024)).toBe("1.5 MB");
  });

  it("formats gigabytes", () => {
    expect(formatBytes(1024 ** 3)).toBe("1 GB");
  });

  it("formats terabytes", () => {
    expect(formatBytes(1024 ** 4)).toBe("1 TB");
  });

  it("respects the decimals argument", () => {
    expect(formatBytes(1500, 0)).toBe("1 KB");
    expect(formatBytes(1500, 3)).toBe("1.465 KB");
  });

  it("treats negative decimals as 0", () => {
    expect(formatBytes(1500, -1)).toBe("1 KB");
  });
});

// ─── getIconByValue ───────────────────────────────────────────────────────────

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

// ─── getFileIcon ──────────────────────────────────────────────────────────────

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

// ─── getFileTypeReadable ──────────────────────────────────────────────────────

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

// ─── groupMimeSizeRecord ──────────────────────────────────────────────────────

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
    expect(result.formattedSize[idx]).toBe("2 KB");
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
