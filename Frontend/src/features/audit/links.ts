import type { RouteLocationRaw } from "vue-router";

import type { AuditLogResult } from "@/api/activity";

import { parseAuditMetadata } from "@/composables/useAuditMessage";
import { AuditEventCode } from "@/enums/audit-event-code";
import { EntityType } from "@/enums/entity-type";

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

// Child rows (versions, previews, tags on a file, share links) point at their file.
const FILE_CHILD_TYPES = new Set([
  EntityType.FileVersion,
  EntityType.Preview,
  EntityType.FileTag,
  EntityType.ShareLink,
]);

const PERMANENT_DELETES = new Set([
  AuditEventCode.FileDeletedPermanently,
  AuditEventCode.DirectoryDeletedPermanently,
  AuditEventCode.TagDeletedPermanently,
  AuditEventCode.PlaylistDeletedPermanently,
  AuditEventCode.UserDeleted,
]);

const hasId = (id: string | null | undefined): id is string => Boolean(id) && id !== EMPTY_GUID;

// The entity a row is about for linking and grouping: the file for its children, the folder
// for a policy, otherwise the row's own entity.
export const auditSubjectId = (row: AuditLogResult): string | null => {
  if (FILE_CHILD_TYPES.has(row.entityType) || row.entityType === EntityType.Policy) {
    if (hasId(row.parentEntityId)) return row.parentEntityId;
  }

  if (hasId(row.entityId)) return row.entityId;

  return null;
};

// Only renames put the entity's own name in "new"; moves and other changes put other values there.
const RENAME_CODES = new Set([
  AuditEventCode.FileRenamed,
  AuditEventCode.DirectoryRenamed,
  AuditEventCode.TagRenamed,
  AuditEventCode.PlaylistRenamed,
]);

const metadataName = (row: AuditLogResult): string | null => {
  const metadata = parseAuditMetadata(row.metadata);
  if (!metadata) return null;

  let name = metadata.name;
  if (RENAME_CODES.has(row.eventCode) && typeof metadata.new === "string") name = metadata.new;

  if (typeof name !== "string" || !name.trim()) return null;

  return name.trim();
};

// Names a row safely: the recorded name snapshot first, which survives deletes, then the
// name the trigger wrote into the metadata.
export const auditEntityName = (row: AuditLogResult): string | null => {
  // A file tag row is named after its tag; the link leads to the file, so that name shows.
  if (row.entityType === EntityType.FileTag) {
    const fileName = parseAuditMetadata(row.metadata)?.file_name;
    if (typeof fileName === "string" && fileName.trim()) return fileName.trim();
  }

  const recorded = row.entityName?.trim();
  if (recorded) return recorded;

  return metadataName(row);
};

// Entities removed for good, as far as the loaded rows can tell, render as plain text.
export const collectGoneEntityIds = (rows: AuditLogResult[]): Set<string> => {
  const gone = new Set<string>();

  for (const row of rows) {
    if (PERMANENT_DELETES.has(row.eventCode) && hasId(row.entityId)) gone.add(row.entityId);
  }

  return gone;
};

// Viewers only follow links into their own data; another user's folder would not open.
const ownedByViewer = (row: AuditLogResult, viewerId: string | undefined): boolean => {
  if (!viewerId) return false;
  if (row.ownerId) return row.ownerId === viewerId;

  return row.userId === viewerId;
};

// Where the expanded details may lead. Files have no page of their own, so they get no link.
export const auditEntityRoute = (
  row: AuditLogResult,
  viewerId: string | undefined,
  gone: Set<string>,
): RouteLocationRaw | null => {
  if (PERMANENT_DELETES.has(row.eventCode) || !ownedByViewer(row, viewerId)) return null;

  const subject = auditSubjectId(row);
  if (!subject || gone.has(subject)) return null;

  if (row.entityType === EntityType.Directory || row.entityType === EntityType.Policy) {
    return { name: "dashboard", params: { dirId: subject } };
  }

  if (row.entityType === EntityType.Playlist) {
    return { name: "playlist-details", params: { id: subject } };
  }

  if (row.entityType === EntityType.Tag) return { name: "tags" };

  return null;
};
