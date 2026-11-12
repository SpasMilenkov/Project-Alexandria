import { AuditEventCode } from "@/enums/audit-event-code";
import { formatDateTime } from "@/utils/date-formatters";
import { logger } from "@/utils/logger";
import { formatBytes } from "@/utils/size";

const AUDIT_EVENT_MESSAGES: Record<AuditEventCode, string> = {
  // File events
  [AuditEventCode.FileCreated]: "File created",
  [AuditEventCode.FileRenamed]: "File renamed",
  [AuditEventCode.FileMoved]: "File moved",
  [AuditEventCode.FileSoftDeleted]: "File moved to trash",
  [AuditEventCode.FileRestored]: "File restored from trash",
  [AuditEventCode.FileDeletedPermanently]: "File permanently deleted",

  // FileVersion events
  [AuditEventCode.FileVersionCreated]: "Version created",
  [AuditEventCode.FileVersionSoftDeleted]: "Version moved to trash",
  [AuditEventCode.FileVersionRestored]: "Version restored from trash",
  [AuditEventCode.FileVersionDeletedPermanently]: "Version permanently deleted",

  // Directory events
  [AuditEventCode.DirectoryCreated]: "Folder created",
  [AuditEventCode.DirectoryRenamed]: "Folder renamed",
  [AuditEventCode.DirectoryMoved]: "Folder moved",
  [AuditEventCode.DirectorySoftDeleted]: "Folder moved to trash",
  [AuditEventCode.DirectoryRestored]: "Folder restored from trash",
  [AuditEventCode.DirectoryDeletedPermanently]: "Folder permanently deleted",

  // Preview events
  [AuditEventCode.PreviewCreated]: "Preview generated",
  [AuditEventCode.PreviewSoftDeleted]: "Preview removed",
  [AuditEventCode.PreviewRestored]: "Preview restored",
  [AuditEventCode.PreviewDeletedPermanently]: "Preview permanently deleted",

  // Tag events
  [AuditEventCode.TagCreated]: "Tag created",
  [AuditEventCode.TagRenamed]: "Tag renamed",
  [AuditEventCode.TagUpdated]: "Tag updated",
  [AuditEventCode.TagSoftDeleted]: "Tag moved to trash",
  [AuditEventCode.TagRestored]: "Tag restored from trash",
  [AuditEventCode.TagDeletedPermanently]: "Tag permanently deleted",

  // User events
  [AuditEventCode.UserCreated]: "Account created",
  [AuditEventCode.UserDeleted]: "Account deleted",
  [AuditEventCode.UserLogin]: "Signed in",
  [AuditEventCode.UserEmailChanged]: "Email address changed",
  [AuditEventCode.UserPasswordChanged]: "Password changed",
  [AuditEventCode.UserLockedOut]: "Account locked out",
  [AuditEventCode.UserLockoutLifted]: "Account lockout lifted",
  [AuditEventCode.UserTwoFactorEnabled]: "Two-factor authentication enabled",
  [AuditEventCode.UserTwoFactorDisabled]: "Two-factor authentication disabled",

  // Sentinel
  [AuditEventCode.Unknown]: "Unknown action",

  // User lifecycle
  [AuditEventCode.UserSoftDeleted]: "Account deactivated",
  [AuditEventCode.UserRestored]: "Account reactivated",
  [AuditEventCode.UserRenamed]: "Account name changed",
  [AuditEventCode.UserQuotaChanged]: "Storage quota changed",

  // File changes
  [AuditEventCode.FileActiveVersionChanged]: "Active version changed",
  [AuditEventCode.FileTypeChanged]: "File type changed",

  // Tag changes
  [AuditEventCode.TagReparented]: "Tag moved to another parent",

  // Custom track covers
  [AuditEventCode.TrackCoverSet]: "Cover art set",
  [AuditEventCode.TrackCoverRemoved]: "Cover art removed",

  // Authentication
  [AuditEventCode.UserLoginFailed]: "Failed sign-in attempt",
  [AuditEventCode.UserLogout]: "Signed out",

  // Access
  [AuditEventCode.FileDownloaded]: "File downloaded",
  [AuditEventCode.FileVersionDownloaded]: "Version downloaded",
  [AuditEventCode.FilesDownloadedBulk]: "Files downloaded as an archive",

  // Bulk summaries
  [AuditEventCode.FilesMovedBulk]: "Files moved",
  [AuditEventCode.FilesDeletedBulk]: "Files deleted",
  [AuditEventCode.FilesCopied]: "Files copied",

  // Admin actions
  [AuditEventCode.JobRequeued]: "Job requeued",
  [AuditEventCode.JobsRequeuedBulk]: "Failed jobs requeued",
  [AuditEventCode.IncidentResolved]: "Incident resolved",

  // Media metadata and lyrics edits
  [AuditEventCode.MediaMetadataEdited]: "Track details edited",
  [AuditEventCode.MediaMetadataEditedBulk]: "Track details edited in bulk",
  [AuditEventCode.LyricsUploaded]: "Lyrics uploaded",
  [AuditEventCode.LyricsDeleted]: "Lyrics deleted",
  [AuditEventCode.LyricsProviderChanged]: "Lyrics source changed",
  [AuditEventCode.LyricsRefetchQueued]: "Lyrics lookup requested",

  // Share links
  [AuditEventCode.ShareLinkCreated]: "Share link created",
  [AuditEventCode.ShareLinkRevoked]: "Share link revoked",
  [AuditEventCode.ShareLinkAccessed]: "Share link used",
  [AuditEventCode.ShareLinkVersionChanged]: "Share link version changed",

  // Roles
  [AuditEventCode.UserRoleGranted]: "Role granted",
  [AuditEventCode.UserRoleRevoked]: "Role removed",

  // File tags
  [AuditEventCode.FileTagApplied]: "Tag added to a file",
  [AuditEventCode.FileTagRemoved]: "Tag removed from a file",

  // Playlists
  [AuditEventCode.PlaylistCreated]: "Playlist created",
  [AuditEventCode.PlaylistRenamed]: "Playlist renamed",
  [AuditEventCode.PlaylistSoftDeleted]: "Playlist moved to trash",
  [AuditEventCode.PlaylistRestored]: "Playlist restored",
  [AuditEventCode.PlaylistDeletedPermanently]: "Playlist permanently deleted",
  [AuditEventCode.PlaylistItemsChanged]: "Playlist tracks changed",

  // Directory policies
  [AuditEventCode.PolicyCreated]: "Folder policy created",
  [AuditEventCode.PolicyUpdated]: "Folder policy updated",
  [AuditEventCode.PolicyDeleted]: "Folder policy removed",
  [AuditEventCode.PolicyRuleCreated]: "Policy rule added",
  [AuditEventCode.PolicyRuleUpdated]: "Policy rule changed",
  [AuditEventCode.PolicyRuleDeleted]: "Policy rule removed",

  // Settings
  [AuditEventCode.SettingChanged]: "Setting changed",
  [AuditEventCode.AdminSettingChanged]: "System setting changed",

  // Sessions and retention
  [AuditEventCode.UserSessionRevoked]: "Session signed out",
  [AuditEventCode.AuditLogsPurged]: "Old activity purged",
};

export type MetadataRenderer =
  | { type: "diff"; label: string; old: string; new: string }
  | { type: "none" };

export interface MetadataEntry {
  label: string;
  value: string;
}

type DiffFormat = "text" | "bytes" | "version";

interface DiffSpec {
  label: string;
  format?: DiffFormat;
  // Shown when one side of the change is empty, such as a tag without a parent.
  empty?: string;
}

// Events whose metadata carries old/new display values.
const DIFF_SPECS: Partial<Record<AuditEventCode, DiffSpec>> = {
  [AuditEventCode.FileRenamed]: { label: "Name" },
  [AuditEventCode.FileMoved]: { empty: "Top level", label: "Location" },
  [AuditEventCode.FileTypeChanged]: { label: "Type" },
  [AuditEventCode.FileActiveVersionChanged]: { format: "version", label: "Active version" },
  [AuditEventCode.DirectoryRenamed]: { label: "Name" },
  [AuditEventCode.DirectoryMoved]: { empty: "Top level", label: "Location" },
  [AuditEventCode.TagRenamed]: { label: "Name" },
  [AuditEventCode.TagReparented]: { empty: "No parent", label: "Parent" },
  [AuditEventCode.UserEmailChanged]: { label: "Email" },
  [AuditEventCode.UserRenamed]: { label: "Name" },
  [AuditEventCode.UserQuotaChanged]: { empty: "Unlimited", format: "bytes", label: "Quota" },
  [AuditEventCode.PlaylistRenamed]: { label: "Name" },
  [AuditEventCode.ShareLinkVersionChanged]: {
    empty: "Latest version",
    format: "version",
    label: "Version",
  },
  [AuditEventCode.LyricsProviderChanged]: { label: "Source" },
};

// Keys that are already on screen elsewhere (name, ids) or are internal plumbing.
const HIDDEN_KEYS = new Set(["name", "old", "new", "file_name"]);

const KEY_LABELS: Record<string, string> = {
  access_count: "Times used",
  action_type: "Action",
  apply_on_new_version: "Applies to new versions",
  changes: "Changes",
  code: "Code",
  color_changed: "Color changed",
  confidence: "Confidence",
  cutoff: "Removed rows older than",
  deleted_at: "Deleted at",
  deleted_count: "Rows removed",
  description_changed: "Description changed",
  destination_name: "Destination",
  directory_count: "Folders",
  email: "Email used",
  enabled: "Enabled",
  end: "Locked until",
  exhausted: "Use limit reached",
  expires_at: "Expires",
  extended: "Lockout extended",
  facet_changed: "Facet changed",
  failed_count: "Failed",
  field: "Field",
  fields: "Fields",
  file_count: "Files",
  hard_delete: "Deleted permanently",
  has_plain: "Plain lyrics",
  has_synced: "Synced lyrics",
  icon_changed: "Icon changed",
  inherited_by_children: "Applies to subfolders",
  ip_address: "IP address",
  job_type: "Job type",
  key: "Setting",
  kind: "Kind",
  limit: "Limit",
  max_access_count: "Use limit",
  mime_type: "Type",
  new_ip: "New IP address",
  note: "Note",
  previous_end: "Was locked until",
  priority: "Priority",
  provider: "Provider",
  reason: "Reason",
  replaced: "Replaced existing",
  requeued: "Requeued",
  reset: "Reset to default",
  retention_days: "Retention (days)",
  role: "Role",
  service: "Service",
  session_started_at: "Session started",
  severity: "Severity",
  size: "Size",
  source: "Source",
  started_at: "Started",
  storage_quota: "Storage quota",
  trigger_type: "Trigger",
  trigger_value: "Trigger value",
  user_agent: "Browser",
  username: "Username",
  version_count: "Versions",
  version_number: "Version",
  added: "Added",
  removed: "Removed",
};

const REASON_LABELS: Record<string, string> = {
  bad_password: "Wrong password",
  locked_out: "Account locked",
  not_allowed: "Sign-in not allowed",
  unknown_user: "No account with that email",
};

const BYTE_KEYS = new Set(["size", "storage_quota"]);

const ISO_DATE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/u;

const STRING_TO_CODE: Record<string, AuditEventCode> = Object.fromEntries(
  Object.entries(AuditEventCode)
    .filter(([, v]) => typeof v === "number")
    .map(([k, v]) => [k, v as AuditEventCode]),
);

// Older clients and exports may carry the enum name instead of the number.
const normalizeCode = (raw: AuditEventCode | string): AuditEventCode => {
  if (typeof raw === "number") return raw;

  return STRING_TO_CODE[raw] ?? AuditEventCode.Unknown;
};

export const parseAuditMetadata = (
  metadataJson?: string | null,
): Record<string, unknown> | null => {
  if (!metadataJson) return null;

  try {
    const parsed: unknown = JSON.parse(metadataJson);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return null;

    return parsed as Record<string, unknown>;
  } catch (err) {
    // Metadata is free-form JSON written by triggers; an unreadable row shows no details.
    logger.warn("Could not read audit metadata", err);
    return null;
  }
};

const humanizeKey = (key: string): string => {
  const known = KEY_LABELS[key];
  if (known) return known;

  const words = key.replaceAll("_", " ");

  return words.charAt(0).toUpperCase() + words.slice(1);
};

const isIdKey = (key: string): boolean =>
  key === "id" || key.endsWith("_id") || key.endsWith("_ids");

const formatScalar = (key: string, value: unknown): string | null => {
  if (value === null || value === undefined || value === "") return null;
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "number" && BYTE_KEYS.has(key)) return formatBytes(value);
  if (typeof value === "number") return value.toLocaleString();
  if (typeof value !== "string") return null;
  if (key === "reason") return REASON_LABELS[value] ?? value;
  if (ISO_DATE.test(value)) return formatDateTime(value);

  return value;
};

const formatListValue = (value: unknown[]): string | null => {
  const items = value.filter((item) => typeof item === "string" || typeof item === "number");
  if (!items.length) return null;

  return items.join(", ");
};

const formatChangeSide = (value: unknown): string => {
  if (value === null || value === undefined || value === "") return "none";
  if (typeof value === "boolean") return value ? "yes" : "no";
  if (typeof value === "object") return "changed";

  return String(value);
};

// Settings, policy rules and track details record changes as { field: { old, new } }.
const changeEntries = (changes: unknown): MetadataEntry[] => {
  if (!changes || typeof changes !== "object" || Array.isArray(changes)) return [];

  return Object.entries(changes as Record<string, unknown>).map(([field, change]) => {
    const label = humanizeKey(field);
    if (!change || typeof change !== "object" || Array.isArray(change)) {
      return { label, value: formatChangeSide(change) };
    }

    const side = change as Record<string, unknown>;
    if (!("old" in side) && !("new" in side)) return { label, value: "changed" };

    return { label, value: `${formatChangeSide(side.old)} → ${formatChangeSide(side.new)}` };
  });
};

const formatDiffSide = (value: unknown, spec: DiffSpec): string => {
  if (value === null || value === undefined || value === "") return spec.empty ?? "None";
  if (spec.format === "bytes" && typeof value === "number") return formatBytes(value);
  if (spec.format === "version") return `v${String(value)}`;

  return String(value);
};

export const getAuditMessage = (raw: AuditEventCode | string): string => {
  const code = normalizeCode(raw);

  return AUDIT_EVENT_MESSAGES[code] ?? AUDIT_EVENT_MESSAGES[AuditEventCode.Unknown];
};

export const getAuditDiff = (
  raw: AuditEventCode | string,
  metadataJson?: string | null,
): MetadataRenderer => {
  const spec = DIFF_SPECS[normalizeCode(raw)];
  const metadata = parseAuditMetadata(metadataJson);
  if (!spec || !metadata) return { type: "none" };
  if (!("old" in metadata) && !("new" in metadata)) return { type: "none" };

  const oldValue = formatDiffSide(metadata.old, spec);
  const newValue = formatDiffSide(metadata.new, spec);
  if (oldValue === newValue) return { type: "none" };

  return { label: spec.label, new: newValue, old: oldValue, type: "diff" };
};

// Everything else worth showing from the metadata, as label/value pairs.
export const getAuditMetadataEntries = (metadataJson?: string | null): MetadataEntry[] => {
  const metadata = parseAuditMetadata(metadataJson);
  if (!metadata) return [];

  return Object.entries(metadata)
    .filter(([key]) => !HIDDEN_KEYS.has(key) && !isIdKey(key))
    .flatMap(([key, value]): MetadataEntry[] => {
      if (key === "changes") return changeEntries(value);

      const formatted = Array.isArray(value) ? formatListValue(value) : formatScalar(key, value);
      if (formatted === null) return [];

      return [{ label: humanizeKey(key), value: formatted }];
    });
};

export const useAuditMessage = () => ({
  getMessage: getAuditMessage,
  getMetadataEntries: getAuditMetadataEntries,
  getRenderedMetadata: getAuditDiff,
});
