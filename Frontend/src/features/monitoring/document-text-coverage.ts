import type { DocumentTextFailure, DocumentTextStatusCount } from "@/api/documentTextStats";

import { DocumentTextStatus } from "@/enums";
import { formatCount } from "@/utils/count";
import { getFileTypeReadable } from "@/utils/files/mimetype";

export interface UnsearchableReason {
  count: number;
  description: string;
  icon: string;
  label: string;
  status: DocumentTextStatus;
}

// Outcomes that are final: the document was read, but there is no text to search.
const UNSEARCHABLE: Partial<
  Record<DocumentTextStatus, Omit<UnsearchableReason, "count" | "status">>
> = {
  [DocumentTextStatus.NoTextLayer]: {
    description: "Pages are images, like a scan. Finding words in them would need OCR.",
    icon: "i-lucide-scan-text",
    label: "Scanned without text",
  },
  [DocumentTextStatus.PasswordProtected]: {
    description: "Opening them needs the document's password.",
    icon: "i-lucide-lock-keyhole",
    label: "Password protected",
  },
  [DocumentTextStatus.CopyProtected]: {
    description: "A store's copy protection encrypts the chapters.",
    icon: "i-lucide-shield-ban",
    label: "Copy protected",
  },
  [DocumentTextStatus.Empty]: {
    description: "Read fine, but hold no text, like a blank page or an empty sheet.",
    icon: "i-lucide-file-x",
    label: "No text",
  },
};

const UNSEARCHABLE_ORDER = [
  DocumentTextStatus.NoTextLayer,
  DocumentTextStatus.PasswordProtected,
  DocumentTextStatus.CopyProtected,
  DocumentTextStatus.Empty,
];

export const statusCount = (counts: DocumentTextStatusCount[], status: DocumentTextStatus) =>
  counts.find((entry) => entry.status === status)?.count ?? 0;

// Only the reasons that occur, most actionable first.
export const unsearchableReasons = (counts: DocumentTextStatusCount[]): UnsearchableReason[] =>
  UNSEARCHABLE_ORDER.flatMap((status) => {
    const meta = UNSEARCHABLE[status];
    const count = statusCount(counts, status);
    if (!meta || count === 0) {
      return [];
    }
    return [{ ...meta, count, status }];
  });

export const unsearchableTotal = (counts: DocumentTextStatusCount[]) =>
  unsearchableReasons(counts).reduce((sum, reason) => sum + reason.count, 0);

export const failureTitle = (failure: DocumentTextFailure) =>
  failure.fileName ?? "A file in another account";

// The server withholds the error for another account's file along with its name.
export const failureDetailLabel = (failure: DocumentTextFailure) => {
  if (failure.fileName === null) {
    return "Details hidden for another account";
  }
  return failure.errorDetail ?? "No error recorded";
};

export const failureTypeLabel = (failure: DocumentTextFailure) =>
  failure.mimeType ? getFileTypeReadable(failure.mimeType, failure.fileName ?? undefined) : null;

// A reset failure has no attempts yet, so the sweep reads it on its next cycle.
export const failureAttemptLabel = (failure: DocumentTextFailure, maxAttempts: number) => {
  if (failure.attemptCount === 0) {
    return "Queued for another try";
  }
  if (!failure.willRetry) {
    return `Gave up after ${formatCount(failure.attemptCount, "try", "tries")}`;
  }
  return `Try ${failure.attemptCount} of ${maxAttempts}, will retry`;
};
