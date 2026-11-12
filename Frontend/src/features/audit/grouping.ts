import type { AuditLogResult } from "@/api/activity";

import { getAuditMessage, parseAuditMetadata } from "@/composables/useAuditMessage";
import { AuditEventCode } from "@/enums/audit-event-code";
import { formatCount } from "@/utils/count";

import { auditSubjectId } from "./links";

export type AuditTimelineItem =
  | { kind: "event"; key: string; row: AuditLogResult }
  | {
      kind: "group";
      key: string;
      correlationId: string;
      rows: AuditLogResult[];
      // The bulk summary row, when the request wrote one.
      summary: AuditLogResult | null;
      title: string;
    };

const BULK_SUMMARY_CODES = new Set([
  AuditEventCode.FilesMovedBulk,
  AuditEventCode.FilesDeletedBulk,
  AuditEventCode.FilesCopied,
  AuditEventCode.FilesDownloadedBulk,
  AuditEventCode.MediaMetadataEditedBulk,
  AuditEventCode.JobsRequeuedBulk,
]);

const UPLOAD_CODES = new Set([AuditEventCode.FileCreated, AuditEventCode.FileVersionCreated]);

// Plural phrasings for the codes that most often arrive many at a time.
const COUNT_TITLES: Partial<Record<AuditEventCode, (count: number) => string>> = {
  [AuditEventCode.FileMoved]: (n) => `Moved ${formatCount(n, "file")}`,
  [AuditEventCode.FileSoftDeleted]: (n) => `Moved ${formatCount(n, "file")} to trash`,
  [AuditEventCode.FileRestored]: (n) => `Restored ${formatCount(n, "file")}`,
  [AuditEventCode.FileDeletedPermanently]: (n) => `Deleted ${formatCount(n, "file")} for good`,
  [AuditEventCode.FileVersionSoftDeleted]: (n) => `Moved ${formatCount(n, "version")} to trash`,
  [AuditEventCode.FileVersionDeletedPermanently]: (n) =>
    `Deleted ${formatCount(n, "version")} for good`,
  [AuditEventCode.DirectoryCreated]: (n) => `Created ${formatCount(n, "folder")}`,
  [AuditEventCode.DirectorySoftDeleted]: (n) => `Moved ${formatCount(n, "folder")} to trash`,
  [AuditEventCode.DirectoryRestored]: (n) => `Restored ${formatCount(n, "folder")}`,
  [AuditEventCode.DirectoryMoved]: (n) => `Moved ${formatCount(n, "folder")}`,
  [AuditEventCode.FileTagApplied]: (n) => `Added ${formatCount(n, "tag")}`,
  [AuditEventCode.FileTagRemoved]: (n) => `Removed ${formatCount(n, "tag")}`,
  [AuditEventCode.MediaMetadataEdited]: (n) => `Edited details of ${formatCount(n, "track")}`,
  [AuditEventCode.FileDownloaded]: (n) => `Downloaded ${formatCount(n, "file")}`,
};

const numberField = (metadata: Record<string, unknown> | null, key: string): number | null => {
  const value = metadata?.[key];

  return typeof value === "number" ? value : null;
};

const stringField = (metadata: Record<string, unknown> | null, key: string): string | null => {
  const value = metadata?.[key];

  return typeof value === "string" && value.trim() ? value.trim() : null;
};

const withDestination = (title: string, destination: string | null): string => {
  if (!destination) return title;

  return `${title} to ${destination}`;
};

// "Moved 37 files to Music", read from the summary row the request wrote.
export const bulkSummaryTitle = (summary: AuditLogResult): string => {
  const metadata = parseAuditMetadata(summary.metadata);
  const files = numberField(metadata, "file_count");
  const destination = summary.entityName ?? stringField(metadata, "destination_name");

  if (summary.eventCode === AuditEventCode.FilesMovedBulk && files !== null) {
    return withDestination(`Moved ${formatCount(files, "file")}`, destination);
  }

  if (summary.eventCode === AuditEventCode.FilesCopied && files !== null) {
    return withDestination(`Copied ${formatCount(files, "file")}`, destination);
  }

  if (summary.eventCode === AuditEventCode.FilesDeletedBulk && files !== null) {
    if (metadata?.hard_delete === true) return `Deleted ${formatCount(files, "file")} for good`;

    return `Moved ${formatCount(files, "file")} to trash`;
  }

  if (summary.eventCode === AuditEventCode.FilesDownloadedBulk) {
    const folders = numberField(metadata, "directory_count") ?? 0;
    const parts = [
      files ? formatCount(files, "file") : null,
      folders ? formatCount(folders, "folder") : null,
    ];
    const counted = parts.filter((part): part is string => part !== null);

    if (counted.length) return `Downloaded ${counted.join(" and ")}`;
  }

  if (summary.eventCode === AuditEventCode.MediaMetadataEditedBulk && files !== null) {
    return `Edited details of ${formatCount(files, "track")}`;
  }

  if (summary.eventCode === AuditEventCode.JobsRequeuedBulk) {
    const requeued = numberField(metadata, "requeued");
    if (requeued !== null) return `Requeued ${formatCount(requeued, "failed job")}`;
  }

  return getAuditMessage(summary.eventCode);
};

const countByCode = (rows: AuditLogResult[]): Map<AuditEventCode, number> => {
  const counts = new Map<AuditEventCode, number>();

  for (const row of rows) counts.set(row.eventCode, (counts.get(row.eventCode) ?? 0) + 1);

  return counts;
};

const uploadTitle = (counts: Map<AuditEventCode, number>): string => {
  const files = counts.get(AuditEventCode.FileCreated) ?? 0;
  if (files > 0) return `Uploaded ${formatCount(files, "file")}`;

  return `Uploaded ${formatCount(counts.get(AuditEventCode.FileVersionCreated) ?? 0, "new version")}`;
};

// Without a summary row the title counts the most common action in the request.
export const groupTitle = (rows: AuditLogResult[]): string => {
  const summary = rows.find((row) => BULK_SUMMARY_CODES.has(row.eventCode));
  if (summary) return bulkSummaryTitle(summary);

  const counts = countByCode(rows);
  if ([...counts.keys()].every((code) => UPLOAD_CODES.has(code))) return uploadTitle(counts);

  const [topCode, topCount] = [...counts.entries()].sort((a, b) => b[1] - a[1])[0]!;
  const phrase = COUNT_TITLES[topCode];
  const base = phrase ? phrase(topCount) : `${getAuditMessage(topCode)} (${topCount})`;
  const others = rows.length - topCount;
  if (others === 0) return base;

  return `${base} and ${formatCount(others, "other change")}`;
};

// A request that touched a single thing (one upload: file + first version) stays as rows.
const touchesSeveralEntities = (rows: AuditLogResult[]): boolean => {
  const subjects = new Set<string>();

  for (const row of rows) {
    const subject = auditSubjectId(row) ?? row.id;
    subjects.add(subject);
    if (subjects.size > 1) return true;
  }

  return false;
};

const flushRun = (items: AuditTimelineItem[], run: AuditLogResult[]) => {
  const first = run[0];
  if (!first) return;

  const correlationId = first.correlationId;
  if (correlationId && run.length > 1 && touchesSeveralEntities(run)) {
    items.push({
      correlationId,
      key: `group-${correlationId}-${first.id}`,
      kind: "group",
      rows: [...run],
      summary: run.find((row) => BULK_SUMMARY_CODES.has(row.eventCode)) ?? null,
      title: groupTitle(run),
    });
    return;
  }

  for (const row of run) items.push({ key: row.id, kind: "event", row });
};

// Collapses consecutive rows from the same request into one summary item.
export const buildAuditTimeline = (rows: AuditLogResult[]): AuditTimelineItem[] => {
  const items: AuditTimelineItem[] = [];
  let run: AuditLogResult[] = [];

  for (const row of rows) {
    const current = run[0];
    const sameRequest =
      Boolean(current && row.correlationId) && row.correlationId === current?.correlationId;

    if (sameRequest) {
      run.push(row);
    } else {
      flushRun(items, run);
      run = [row];
    }
  }

  flushRun(items, run);

  return items;
};
