import type { AuditExportBounds, AuditExportPart } from "@/api/activity";

import { AuditExportFormat } from "@/enums/audit-export-format";
import { formatCount } from "@/utils/count";
import { formatCalendarDateRange } from "@/utils/date-formatters";

export const EXPORT_EXTENSIONS: Record<AuditExportFormat, string> = {
  [AuditExportFormat.Csv]: "csv",
  [AuditExportFormat.Json]: "json",
};

// Only the bounds go back to the server; the rest of the part is for display.
export const exportBounds = (part: AuditExportPart): AuditExportBounds => ({
  endAt: part.endAt,
  endId: part.endId,
  startAt: part.startAt,
  startId: part.startId,
});

export const exportFileName = (
  baseName: string,
  stamp: string,
  format: AuditExportFormat,
  part?: { index: number; count: number },
): string => {
  const extension = EXPORT_EXTENSIONS[format];
  if (!part || part.count <= 1) return `${baseName}-${stamp}.${extension}`;

  return `${baseName}-${stamp}-part-${part.index}-of-${part.count}.${extension}`;
};

// Parts run newest first, so the range reads from the part's oldest row to its newest.
export const exportPartSummary = (part: AuditExportPart): string =>
  `${formatCount(part.rows, "row")} · ${formatCalendarDateRange(part.oldestAt, part.startAt)}`;
