import { describe, it, expect } from "vitest";

import { getFileTypeReadable, groupMimeSizeRecord } from "@/utils/mimetype.utils";

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
