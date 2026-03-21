import { AuditEventCode } from "@/api/activity";

// Human-readable titles

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
};

// Metadata rendering

export type MetadataRenderer =
  | { type: "diff"; label: string; old: string; new: string }
  | { type: "none" };

// Events where metadataJson carries a meaningful old/new diff
const DIFF_LABELS: Partial<Record<AuditEventCode, string>> = {
  [AuditEventCode.FileRenamed]: "Name",
  [AuditEventCode.FileMoved]: "Location",
  [AuditEventCode.DirectoryRenamed]: "Name",
  [AuditEventCode.DirectoryMoved]: "Location",
  [AuditEventCode.TagRenamed]: "Name",
  [AuditEventCode.TagUpdated]: "Value",
  [AuditEventCode.UserEmailChanged]: "Email",
  [AuditEventCode.UserPasswordChanged]: "Password",
};

// String-name normalizer
// The backend may serialize AuditEventCode as its string name ("DirectoryRenamed")
// rather than the numeric value. This builds a reverse map so both forms resolve.

const STRING_TO_CODE: Record<string, AuditEventCode> = Object.fromEntries(
  Object.entries(AuditEventCode)
    .filter(([, v]) => typeof v === "number")
    .map(([k, v]) => [k, v as AuditEventCode]),
);

const normalizeCode = (raw: AuditEventCode | string): AuditEventCode => {
  if (typeof raw === "number") return raw;
  return STRING_TO_CODE[raw] ?? AuditEventCode.Unknown;
};

// Composable

export const useAuditMessage = () => {
  const getMessage = (raw: AuditEventCode | string): string => {
    const code = normalizeCode(raw);
    return AUDIT_EVENT_MESSAGES[code] ?? AUDIT_EVENT_MESSAGES[AuditEventCode.Unknown];
  };

  const getRenderedMetadata = (
    raw: AuditEventCode | string,
    metadataJson?: string,
  ): MetadataRenderer => {
    const code = normalizeCode(raw);
    const label = DIFF_LABELS[code];
    if (!label || !metadataJson) return { type: "none" };

    try {
      const parsed = JSON.parse(metadataJson) as Record<string, unknown>;

      const oldVal = String(parsed.old ?? "");
      const newVal = String(parsed.new ?? "");

      if (!oldVal && !newVal) return { type: "none" };

      return { label, new: newVal, old: oldVal, type: "diff" };
    } catch {
      return { type: "none" };
    }
  };

  return { getMessage, getRenderedMetadata };
};
