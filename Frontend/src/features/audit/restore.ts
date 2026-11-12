import type { AuditLogResult } from "@/api/activity";
import type { AuditRestoreTarget } from "@/mutations/activity";

import { AuditEventCode } from "@/enums/audit-event-code";

// Matches TrashRetentionPolicy on the backend: trashed items stay restorable this long.
export const TRASH_RETENTION_DAYS = 30;

const DAY_MS = 86_400_000;

// Rows that end a soft delete, so an older trash row no longer has anything to restore.
const SETTLING_CODES: Partial<Record<AuditEventCode, AuditEventCode[]>> = {
  [AuditEventCode.FileSoftDeleted]: [
    AuditEventCode.FileRestored,
    AuditEventCode.FileDeletedPermanently,
  ],
  [AuditEventCode.DirectorySoftDeleted]: [
    AuditEventCode.DirectoryRestored,
    AuditEventCode.DirectoryDeletedPermanently,
  ],
  [AuditEventCode.FileVersionSoftDeleted]: [
    AuditEventCode.FileVersionRestored,
    AuditEventCode.FileVersionDeletedPermanently,
  ],
};

export const auditRestoreTarget = (row: AuditLogResult): AuditRestoreTarget | null => {
  if (row.eventCode === AuditEventCode.FileSoftDeleted)
    return { fileId: row.entityId, kind: "file" };

  if (row.eventCode === AuditEventCode.DirectorySoftDeleted) {
    return { directoryId: row.entityId, kind: "directory" };
  }

  if (row.eventCode === AuditEventCode.FileVersionSoftDeleted && row.parentEntityId) {
    return { fileId: row.parentEntityId, kind: "version", versionId: row.entityId };
  }

  return null;
};

// Only the viewer's own trash, still inside the window, and not already settled by a newer
// row the timeline has loaded. Rows are newest first.
export const canRestoreFromAudit = (
  row: AuditLogResult,
  rows: AuditLogResult[],
  viewerId: string | undefined,
  now = Date.now(),
): boolean => {
  const settling = SETTLING_CODES[row.eventCode];
  if (!settling || !viewerId || (row.ownerId ?? row.userId) !== viewerId) return false;

  const deletedAt = Date.parse(row.timestamp);
  const age = now - deletedAt;
  if (!Number.isFinite(age) || age > TRASH_RETENTION_DAYS * DAY_MS) return false;

  return !rows.some(
    (other) =>
      other.entityId === row.entityId &&
      settling.includes(other.eventCode) &&
      Date.parse(other.timestamp) > deletedAt,
  );
};
