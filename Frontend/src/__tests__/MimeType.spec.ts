import { describe, expect, it } from "vitest";

import {
  getFileTypeReadable,
  groupMimeSizeRecord,
  isThumbnailSupportedMimeType,
} from "@/utils/mimetype.utils";

// Documents

describe("getFileTypeReadable – Documents", () => {
  it("handles legacy Word .doc format", () => {
    expect(getFileTypeReadable("application/msword")).toBe("Word Document");
  });

  it("handles modern Word .docx (OOXML) format", () => {
    expect(
      getFileTypeReadable(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      ),
    ).toBe("Word Document");
  });

  it("handles OpenDocument Text (.odt)", () => {
    expect(getFileTypeReadable("application/vnd.oasis.opendocument.text")).toBe("Text Document");
  });

  it("handles Rich Text Format (.rtf)", () => {
    expect(getFileTypeReadable("application/rtf")).toBe("Rich Text Document");
  });

  it("handles Markdown", () => {
    expect(getFileTypeReadable("text/markdown")).toBe("Markdown Document");
  });

  it("handles PDF", () => {
    expect(getFileTypeReadable("application/pdf")).toBe("PDF Document");
  });

  // Fallback grouping for document-like subtypes not in the lookup table
  it("falls back to Documents group for an unknown wordprocessing MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.openxmlformats-officedocument.wordprocessingml.template": 1024,
    });
    expect(result.categories).toContain("Documents");
  });

  it("falls back to Documents group for an unknown document MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.some-vendor.document": 1024,
    });
    expect(result.categories).toContain("Documents");
  });
});

// Spreadsheets

describe("getFileTypeReadable – Spreadsheets", () => {
  it("handles legacy Excel .xls format", () => {
    expect(getFileTypeReadable("application/vnd.ms-excel")).toBe("Spreadsheet");
  });

  it("handles modern Excel .xlsx (OOXML) format", () => {
    expect(
      getFileTypeReadable("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
    ).toBe("Spreadsheet");
  });

  it("handles OpenDocument Spreadsheet (.ods)", () => {
    expect(getFileTypeReadable("application/vnd.oasis.opendocument.spreadsheet")).toBe(
      "Spreadsheet",
    );
  });

  it("handles CSV", () => {
    expect(getFileTypeReadable("text/csv")).toBe("CSV File");
  });

  it("handles TSV", () => {
    expect(getFileTypeReadable("text/tab-separated-values")).toBe("TSV File");
  });

  // Fallback grouping for spreadsheet-like subtypes not in the lookup table
  it("falls back to Spreadsheets group for an Excel macro-enabled workbook MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.ms-excel.sheet.macroEnabled.12": 1024,
    });
    expect(result.categories).toContain("Spreadsheets");
  });

  it("falls back to Spreadsheets group for an unknown sheet MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.some-vendor.spreadsheet": 1024,
    });
    expect(result.categories).toContain("Spreadsheets");
  });

  it("falls back to Spreadsheets group for an OOXML spreadsheet template variant", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.openxmlformats-officedocument.spreadsheetml.template": 1024,
    });
    expect(result.categories).toContain("Spreadsheets");
  });
});

// Presentations

describe("getFileTypeReadable – Presentations", () => {
  it("handles legacy PowerPoint .ppt format", () => {
    expect(getFileTypeReadable("application/vnd.ms-powerpoint")).toBe("Presentation");
  });

  it("handles modern PowerPoint .pptx (OOXML) format", () => {
    expect(
      getFileTypeReadable(
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      ),
    ).toBe("Presentation");
  });

  it("handles OpenDocument Presentation (.odp)", () => {
    expect(getFileTypeReadable("application/vnd.oasis.opendocument.presentation")).toBe(
      "Presentation",
    );
  });

  // Fallback grouping for presentation-like subtypes not in the lookup table
  it("falls back to Presentations group for a PowerPoint macro-enabled MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.ms-powerpoint.presentation.macroEnabled.12": 1024,
    });
    expect(result.categories).toContain("Presentations");
  });

  it("falls back to Presentations group for an OOXML slideshow variant", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.openxmlformats-officedocument.presentationml.slideshow": 1024,
    });
    expect(result.categories).toContain("Presentations");
  });

  it("falls back to Presentations group for an unknown presentation MIME", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.some-vendor.presentation": 1024,
    });
    expect(result.categories).toContain("Presentations");
  });
});

// groupMimeSizeRecord cross-group aggregation

describe("groupMimeSizeRecord – cross-office aggregation", () => {
  it("correctly splits .doc, .xlsx and .pptx into three separate groups", () => {
    const result = groupMimeSizeRecord({
      "application/msword": 512,
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": 1024,
      "application/vnd.openxmlformats-officedocument.presentationml.presentation": 2048,
    });

    expect(result.categories).toContain("Documents");
    expect(result.categories).toContain("Spreadsheets");
    expect(result.categories).toContain("Presentations");
    expect(result.categories).toHaveLength(3);
  });

  it("accumulates bytes correctly when mixing legacy and modern document formats", () => {
    const result = groupMimeSizeRecord({
      "application/msword": 1024,
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document": 2048,
      "application/vnd.oasis.opendocument.text": 512,
      "application/rtf": 256,
    });

    const idx = result.categories.indexOf("Documents");
    expect(result.size[idx]).toBe(1024 + 2048 + 512 + 256);
  });

  it("accumulates bytes correctly for all three spreadsheet formats", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.ms-excel": 500,
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": 1500,
      "application/vnd.oasis.opendocument.spreadsheet": 1000,
    });

    const idx = result.categories.indexOf("Spreadsheets");
    expect(result.size[idx]).toBe(3000);
  });

  it("accumulates bytes correctly for all three presentation formats", () => {
    const result = groupMimeSizeRecord({
      "application/vnd.ms-powerpoint": 800,
      "application/vnd.openxmlformats-officedocument.presentationml.presentation": 1200,
      "application/vnd.oasis.opendocument.presentation": 400,
    });

    const idx = result.categories.indexOf("Presentations");
    expect(result.size[idx]).toBe(2400);
  });
});

describe("getFileTypeReadable – content-sniffed additions", () => {
  it("labels Vue components and SQL files as code", () => {
    expect(getFileTypeReadable("text/x-vue")).toBe("Vue Component");
    expect(getFileTypeReadable("application/sql")).toBe("SQL File");
  });

  it("labels EPUB books and Java archives", () => {
    expect(getFileTypeReadable("application/epub+zip")).toBe("EPUB Book");
    expect(getFileTypeReadable("application/java-archive")).toBe("Java Archive");
  });

  it("labels Dockerfiles as code", () => {
    expect(getFileTypeReadable("text/x-dockerfile")).toBe("Dockerfile");
  });

  it("labels container and build files", () => {
    expect(getFileTypeReadable("text/x-containerfile")).toBe("Containerfile");
    expect(getFileTypeReadable("text/x-makefile")).toBe("Makefile");
    expect(getFileTypeReadable("text/x-log")).toBe("Log File");
  });

  it("labels additional language source files", () => {
    expect(getFileTypeReadable("text/x-tex")).toBe("LaTeX Source");
    expect(getFileTypeReadable("text/x-sln")).toBe("Visual Studio Solution");
    expect(getFileTypeReadable("text/x-slnx")).toBe("Visual Studio Solution (XML)");
    expect(getFileTypeReadable("text/x-swift")).toBe("Swift Source File");
    expect(getFileTypeReadable("text/x-scala")).toBe("Scala Source File");
    expect(getFileTypeReadable("text/x-kotlin")).toBe("Kotlin Source File");
    expect(getFileTypeReadable("text/x-kotlin-script")).toBe("Kotlin Script");
    expect(getFileTypeReadable("application/dart")).toBe("Dart Source File");
    expect(getFileTypeReadable("application/x-php")).toBe("PHP Script");
  });

  it("labels the second batch of source and build files", () => {
    expect(getFileTypeReadable("text/x-powershell")).toBe("PowerShell Script");
    expect(getFileTypeReadable("text/x-batch")).toBe("Batch Script");
    expect(getFileTypeReadable("text/x-ruby")).toBe("Ruby Script");
    expect(getFileTypeReadable("text/x-lua")).toBe("Lua Script");
    expect(getFileTypeReadable("text/x-r")).toBe("R Script");
    expect(getFileTypeReadable("text/x-julia")).toBe("Julia Script");
    expect(getFileTypeReadable("text/x-fsharp")).toBe("F# Source File");
    expect(getFileTypeReadable("text/x-vb")).toBe("Visual Basic Source File");
    expect(getFileTypeReadable("text/x-scss")).toBe("SCSS Stylesheet");
    expect(getFileTypeReadable("text/x-sass")).toBe("Sass Stylesheet");
    expect(getFileTypeReadable("text/x-less")).toBe("Less Stylesheet");
    expect(getFileTypeReadable("text/x-hcl")).toBe("Terraform / HCL File");
    expect(getFileTypeReadable("text/x-nix")).toBe("Nix Expression");
    expect(getFileTypeReadable("text/x-cmake")).toBe("CMake File");
    expect(getFileTypeReadable("text/x-bazel")).toBe("Bazel File");
    expect(getFileTypeReadable("text/x-gradle")).toBe("Gradle Build Script");
    expect(getFileTypeReadable("text/x-groovy")).toBe("Groovy Script");
    expect(getFileTypeReadable("application/jsonc")).toBe("JSON with Comments");
    expect(getFileTypeReadable("application/json5")).toBe("JSON5 File");
    expect(getFileTypeReadable("application/x-ndjson")).toBe("JSON Lines File");
    expect(getFileTypeReadable("text/x-diff")).toBe("Diff / Patch File");
  });

  it("groups the new code types under Code", () => {
    const result = groupMimeSizeRecord({
      "text/x-csharp": 128,
      "text/x-vue": 256,
      "text/typescript": 512,
      "text/x-dockerfile": 1024,
      "text/x-containerfile": 64,
      "text/x-makefile": 64,
      "text/x-tex": 64,
      "text/x-sln": 64,
      "text/x-slnx": 64,
      "text/x-swift": 64,
      "text/x-scala": 64,
      "text/x-kotlin": 64,
      "text/x-kotlin-script": 64,
      "application/dart": 64,
      "application/x-php": 64,
      "text/x-powershell": 64,
      "text/x-batch": 64,
      "text/x-ruby": 64,
      "text/x-lua": 64,
      "text/x-r": 64,
      "text/x-julia": 64,
      "text/x-fsharp": 64,
      "text/x-vb": 64,
      "text/x-scss": 64,
      "text/x-sass": 64,
      "text/x-less": 64,
      "text/x-hcl": 64,
      "text/x-nix": 64,
      "text/x-cmake": 64,
      "text/x-bazel": 64,
      "text/x-gradle": 64,
      "text/x-groovy": 64,
      "application/jsonc": 64,
      "application/json5": 64,
      "application/x-ndjson": 64,
      "text/x-diff": 64,
    });

    expect(result.categories).toEqual(["Code"]);
  });

  it("labels media-adjacent text files and dotfiles", () => {
    expect(getFileTypeReadable("text/x-srt")).toBe("SubRip Subtitle");
    expect(getFileTypeReadable("text/x-vtt")).toBe("WebVTT Subtitle");
    expect(getFileTypeReadable("text/x-ass")).toBe("ASS Subtitle");
    expect(getFileTypeReadable("text/x-m3u")).toBe("M3U Playlist");
    expect(getFileTypeReadable("text/x-m3u8")).toBe("M3U8 Playlist");
    expect(getFileTypeReadable("text/x-cue")).toBe("CUE Sheet");
    expect(getFileTypeReadable("text/x-lrc")).toBe("LRC Lyrics");
    expect(getFileTypeReadable("text/x-nfo")).toBe("NFO File");
    expect(getFileTypeReadable("text/x-gitignore")).toBe("Git Ignore File");
    expect(getFileTypeReadable("text/x-gitattributes")).toBe("Git Attributes File");
    expect(getFileTypeReadable("text/x-dockerignore")).toBe("Docker Ignore File");
    expect(getFileTypeReadable("text/x-editorconfig")).toBe("EditorConfig File");
    expect(getFileTypeReadable("text/x-htaccess")).toBe("Apache .htaccess");
    expect(getFileTypeReadable("text/x-nginx")).toBe("Nginx Configuration");
  });

  it("groups media text as Text and server configs as Code", () => {
    const text = groupMimeSizeRecord({
      "text/x-srt": 64,
      "text/x-vtt": 64,
      "text/x-ass": 64,
      "text/x-m3u": 64,
      "text/x-m3u8": 64,
      "text/x-cue": 64,
      "text/x-lrc": 64,
      "text/x-nfo": 64,
      "text/x-gitignore": 64,
      "text/x-gitattributes": 64,
      "text/x-dockerignore": 64,
      "text/x-editorconfig": 64,
    });
    const code = groupMimeSizeRecord({
      "text/x-htaccess": 64,
      "text/x-nginx": 64,
    });

    expect(text.categories).toEqual(["Text"]);
    expect(code.categories).toEqual(["Code"]);
  });

  it("keeps logs in the Text group", () => {
    const result = groupMimeSizeRecord({ "text/x-log": 128 });

    expect(result.categories).toEqual(["Text"]);
  });
});

describe("isThumbnailSupportedMimeType", () => {
  it("supports image, audio, video and office document MIMEs", () => {
    expect(isThumbnailSupportedMimeType("image/png")).toBe(true);
    expect(isThumbnailSupportedMimeType("audio/mpeg")).toBe(true);
    expect(isThumbnailSupportedMimeType("video/mp4")).toBe(true);
    expect(isThumbnailSupportedMimeType("application/pdf")).toBe(true);
    expect(
      isThumbnailSupportedMimeType(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      ),
    ).toBe(true);
  });

  it("rejects text, archive and unknown MIMEs", () => {
    expect(isThumbnailSupportedMimeType("application/json")).toBe(false);
    expect(isThumbnailSupportedMimeType("text/plain")).toBe(false);
    expect(isThumbnailSupportedMimeType("application/zip")).toBe(false);
    expect(isThumbnailSupportedMimeType("application/octet-stream")).toBe(false);
  });

  it("handles empty values and normalizes case and parameters", () => {
    expect(isThumbnailSupportedMimeType(null)).toBe(false);
    expect(isThumbnailSupportedMimeType(undefined)).toBe(false);
    expect(isThumbnailSupportedMimeType("")).toBe(false);
    expect(isThumbnailSupportedMimeType("IMAGE/PNG")).toBe(true);
    expect(isThumbnailSupportedMimeType("image/png; charset=binary")).toBe(true);
  });
});
