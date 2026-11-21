import { describe, expect, it } from "vitest";

import type { DocumentTextFailure } from "@/api/documentTextStats";

import { DocumentTextStatus } from "@/enums";
import {
  failureAttemptLabel,
  failureDetailLabel,
  failureTitle,
  failureTypeLabel,
  statusCount,
  unsearchableReasons,
  unsearchableTotal,
} from "@/features/monitoring/document-text-coverage";

const counts = [
  { count: 12, status: DocumentTextStatus.Extracted },
  { count: 1, status: DocumentTextStatus.Empty },
  { count: 3, status: DocumentTextStatus.NoTextLayer },
  { count: 2, status: DocumentTextStatus.Failed },
];

const failure = (overrides: Partial<DocumentTextFailure> = {}): DocumentTextFailure => ({
  attemptCount: 1,
  contentObjectId: "c1",
  errorDetail: "pdftotext exited with 1",
  fileName: "scan.pdf",
  lastAttemptAt: "2026-11-20T10:00:00Z",
  mimeType: "application/pdf",
  willRetry: true,
  ...overrides,
});

describe("document text coverage", () => {
  it("reads a status count, and zero for one that is missing", () => {
    expect(statusCount(counts, DocumentTextStatus.Extracted)).toBe(12);
    expect(statusCount(counts, DocumentTextStatus.CopyProtected)).toBe(0);
  });

  it("lists only the reasons that occur, scans first", () => {
    const reasons = unsearchableReasons(counts);

    expect(reasons.map((reason) => [reason.label, reason.count])).toEqual([
      ["Scanned without text", 3],
      ["No text", 1],
    ]);
    expect(unsearchableTotal(counts)).toBe(4);
  });

  it("never counts failures or extracted text as unsearchable", () => {
    const reasons = unsearchableReasons(counts).map((reason) => reason.status);

    expect(reasons).not.toContain(DocumentTextStatus.Failed);
    expect(reasons).not.toContain(DocumentTextStatus.Extracted);
  });

  it("names another account's file without its name", () => {
    expect(failureTitle(failure())).toBe("scan.pdf");
    expect(failureTitle(failure({ fileName: null }))).toBe("A file in another account");
  });

  it("shows the error only for the admin's own files", () => {
    expect(failureDetailLabel(failure())).toBe("pdftotext exited with 1");
    expect(failureDetailLabel(failure({ errorDetail: null }))).toBe("No error recorded");
    expect(failureDetailLabel(failure({ errorDetail: null, fileName: null }))).toBe(
      "Details hidden for another account",
    );
  });

  it("describes the file type when it is known", () => {
    expect(failureTypeLabel(failure())).toBeTruthy();
    expect(failureTypeLabel(failure({ mimeType: null }))).toBeNull();
  });

  it("says whether the sweep will try again", () => {
    expect(failureAttemptLabel(failure(), 3)).toBe("Try 1 of 3, will retry");
    expect(failureAttemptLabel(failure({ attemptCount: 3, willRetry: false }), 3)).toBe(
      "Gave up after 3 tries",
    );
    expect(failureAttemptLabel(failure({ attemptCount: 0 }), 3)).toBe("Queued for another try");
  });
});
