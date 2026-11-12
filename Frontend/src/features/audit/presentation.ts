import { AuditEventCode } from "@/enums/audit-event-code";
import { EntityType } from "@/enums/entity-type";
import { LogSource } from "@/enums/log-source";
import { OperationType } from "@/enums/operation-type";

export type AuditBadgeColor = "success" | "error" | "warning" | "info" | "neutral";

export const ENTITY_LABELS: Record<EntityType, string> = {
  [EntityType.File]: "File",
  [EntityType.FileVersion]: "File version",
  [EntityType.Directory]: "Folder",
  [EntityType.Tag]: "Tag",
  [EntityType.User]: "Account",
  [EntityType.Preview]: "Preview",
  [EntityType.ShareLink]: "Share link",
  [EntityType.Playlist]: "Playlist",
  [EntityType.Policy]: "Folder policy",
  [EntityType.Setting]: "Setting",
  [EntityType.FileTag]: "File tag",
  [EntityType.Job]: "Job",
  [EntityType.Incident]: "Incident",
  [EntityType.Session]: "Session",
  [EntityType.System]: "System",
};

const ENTITY_ICONS: Record<EntityType, string> = {
  [EntityType.File]: "i-lucide-file",
  [EntityType.FileVersion]: "i-lucide-file-clock",
  [EntityType.Directory]: "i-lucide-folder",
  [EntityType.Tag]: "i-lucide-tag",
  [EntityType.User]: "i-lucide-user",
  [EntityType.Preview]: "i-lucide-image",
  [EntityType.ShareLink]: "i-lucide-share-2",
  [EntityType.Playlist]: "i-lucide-list-music",
  [EntityType.Policy]: "i-lucide-shield-check",
  [EntityType.Setting]: "i-lucide-settings",
  [EntityType.FileTag]: "i-lucide-tags",
  [EntityType.Job]: "i-lucide-layers",
  [EntityType.Incident]: "i-lucide-triangle-alert",
  [EntityType.Session]: "i-lucide-monitor",
  [EntityType.System]: "i-lucide-cog",
};

// Events whose action says more than the entity type does.
const EVENT_ICONS: Partial<Record<AuditEventCode, string>> = {
  [AuditEventCode.UserLogin]: "i-lucide-log-in",
  [AuditEventCode.UserLogout]: "i-lucide-log-out",
  [AuditEventCode.UserLoginFailed]: "i-lucide-shield-x",
  [AuditEventCode.UserPasswordChanged]: "i-lucide-lock-keyhole",
  [AuditEventCode.UserLockedOut]: "i-lucide-lock",
  [AuditEventCode.UserLockoutLifted]: "i-lucide-lock-open",
  [AuditEventCode.UserTwoFactorEnabled]: "i-lucide-fingerprint",
  [AuditEventCode.UserTwoFactorDisabled]: "i-lucide-fingerprint",
  [AuditEventCode.UserSessionRevoked]: "i-lucide-log-out",
  [AuditEventCode.FileDownloaded]: "i-lucide-download",
  [AuditEventCode.FileVersionDownloaded]: "i-lucide-download",
  [AuditEventCode.FilesDownloadedBulk]: "i-lucide-download",
  [AuditEventCode.FilesMovedBulk]: "i-lucide-folder-tree",
  [AuditEventCode.FilesDeletedBulk]: "i-lucide-trash-2",
  [AuditEventCode.FilesCopied]: "i-lucide-copy",
  [AuditEventCode.LyricsUploaded]: "i-lucide-align-left",
  [AuditEventCode.LyricsDeleted]: "i-lucide-align-left",
  [AuditEventCode.LyricsProviderChanged]: "i-lucide-align-left",
  [AuditEventCode.LyricsRefetchQueued]: "i-lucide-align-left",
  [AuditEventCode.MediaMetadataEdited]: "i-lucide-pencil-line",
  [AuditEventCode.MediaMetadataEditedBulk]: "i-lucide-pencil-line",
  [AuditEventCode.TrackCoverSet]: "i-lucide-image-up",
  [AuditEventCode.TrackCoverRemoved]: "i-lucide-image-off",
  [AuditEventCode.AuditLogsPurged]: "i-lucide-history",
};

export const auditIcon = (eventCode: AuditEventCode, entityType: EntityType): string =>
  EVENT_ICONS[eventCode] ?? ENTITY_ICONS[entityType] ?? "i-lucide-activity";

export const entityLabel = (entityType: EntityType): string => ENTITY_LABELS[entityType] ?? "Item";

export const entityIcon = (entityType: EntityType): string =>
  ENTITY_ICONS[entityType] ?? "i-lucide-activity";

export const OPERATION_LABELS: Record<OperationType, string> = {
  [OperationType.Read]: "Access",
  [OperationType.Create]: "Create",
  [OperationType.Update]: "Update",
  [OperationType.Delete]: "Delete",
  [OperationType.Login]: "Sign-in",
  [OperationType.Logout]: "Sign-out",
};

const OPERATION_COLORS: Record<OperationType, AuditBadgeColor> = {
  [OperationType.Read]: "neutral",
  [OperationType.Create]: "success",
  [OperationType.Update]: "warning",
  [OperationType.Delete]: "error",
  [OperationType.Login]: "info",
  [OperationType.Logout]: "neutral",
};

export const operationColor = (operation: OperationType): AuditBadgeColor =>
  OPERATION_COLORS[operation] ?? "neutral";

const OPERATION_ICONS: Record<OperationType, string> = {
  [OperationType.Read]: "i-lucide-eye",
  [OperationType.Create]: "i-lucide-plus-circle",
  [OperationType.Update]: "i-lucide-pencil",
  [OperationType.Delete]: "i-lucide-trash-2",
  [OperationType.Login]: "i-lucide-log-in",
  [OperationType.Logout]: "i-lucide-log-out",
};

export const operationIcon = (operation: OperationType): string =>
  OPERATION_ICONS[operation] ?? "i-lucide-activity";

export const operationLabel = (operation: OperationType): string =>
  OPERATION_LABELS[operation] ?? "Other";

const NEUTRAL_INDICATOR =
  "bg-gray-500/10 text-gray-600 ring-1 ring-inset ring-gray-400/30 dark:text-gray-400 dark:ring-gray-500/30";

const OPERATION_INDICATORS: Partial<Record<OperationType, string>> = {
  [OperationType.Create]:
    "bg-emerald-500/10 text-emerald-600 ring-1 ring-inset ring-emerald-500/30 dark:text-emerald-400",
  [OperationType.Delete]:
    "bg-rose-500/10 text-rose-600 ring-1 ring-inset ring-rose-500/30 dark:text-rose-400",
  [OperationType.Update]:
    "bg-amber-500/10 text-amber-600 ring-1 ring-inset ring-amber-500/30 dark:text-amber-400",
  [OperationType.Login]:
    "bg-sky-500/10 text-sky-600 ring-1 ring-inset ring-sky-500/30 dark:text-sky-400",
};

// A failed sign-in is a login operation but reads as a warning, not a success.
export const auditIndicatorClass = (
  eventCode: AuditEventCode,
  operation: OperationType,
): string => {
  if (eventCode === AuditEventCode.UserLoginFailed)
    return OPERATION_INDICATORS[OperationType.Delete]!;

  return OPERATION_INDICATORS[operation] ?? NEUTRAL_INDICATOR;
};

export const SOURCE_LABELS: Record<LogSource, string> = {
  [LogSource.API]: "Recorded by the app",
  [LogSource.Trigger]: "Recorded by the database",
  [LogSource.System]: "Recorded by the system",
};

export const sourceLabel = (source: LogSource): string => SOURCE_LABELS[source] ?? "Unknown source";

const SOURCE_ICONS: Record<LogSource, string> = {
  [LogSource.API]: "i-lucide-app-window",
  [LogSource.Trigger]: "i-lucide-database",
  [LogSource.System]: "i-lucide-server-cog",
};

export const sourceIcon = (source: LogSource): string =>
  SOURCE_ICONS[source] ?? "i-lucide-activity";

// What each recorder writes. The app logs what someone meant to do, the database logs every
// row that actually changed, and the system covers work nobody was signed in for.
// Short enough for a dropdown line; the info hint carries the full descriptions.
export const SOURCE_SUMMARIES: Record<LogSource, string> = {
  [LogSource.API]: "Sign-ins, downloads, bulk summaries",
  [LogSource.Trigger]: "Every stored change",
  [LogSource.System]: "Background work",
};

export const SOURCE_DESCRIPTIONS: Record<LogSource, string> = {
  [LogSource.API]:
    "What someone did that changes no stored item, such as sign-ins and downloads, plus one summary for each bulk action.",
  [LogSource.Trigger]:
    "Every item that was created, changed or deleted. The database writes these itself, so no change can skip the log.",
  [LogSource.System]:
    "Work done with nobody signed in, such as previews made in the background and scheduled cleanups.",
};

// Filter options, in the order the toolbars list them.
export const ENTITY_TYPE_OPTIONS: { icon: string; label: string; value: EntityType }[] = [
  EntityType.File,
  EntityType.FileVersion,
  EntityType.Directory,
  EntityType.Tag,
  EntityType.FileTag,
  EntityType.ShareLink,
  EntityType.Playlist,
  EntityType.Policy,
  EntityType.Preview,
  EntityType.User,
  EntityType.Session,
  EntityType.Setting,
  EntityType.Job,
  EntityType.Incident,
  EntityType.System,
].map((value) => ({ icon: ENTITY_ICONS[value], label: ENTITY_LABELS[value], value }));

export const OPERATION_OPTIONS: { icon: string; label: string; value: OperationType }[] = [
  OperationType.Create,
  OperationType.Update,
  OperationType.Delete,
  OperationType.Read,
  OperationType.Login,
  OperationType.Logout,
].map((value) => ({
  icon: OPERATION_ICONS[value],
  label: OPERATION_LABELS[value],
  value,
}));

// Mirrors the backend's AuditPersonalContent. An admin only sees these for their own library,
// which Access History already covers, so the admin explorer does not offer them.
const PERSONAL_CONTENT_TYPES = new Set<EntityType>([
  EntityType.File,
  EntityType.FileVersion,
  EntityType.Directory,
  EntityType.Tag,
  EntityType.Preview,
  EntityType.ShareLink,
  EntityType.Playlist,
  EntityType.Policy,
  EntityType.FileTag,
]);

export const ADMIN_ENTITY_TYPE_OPTIONS = ENTITY_TYPE_OPTIONS.filter(
  (option) => !PERSONAL_CONTENT_TYPES.has(option.value),
);

// Access rows are downloads and share-link visits, which are all library content.
export const ADMIN_OPERATION_OPTIONS = OPERATION_OPTIONS.filter(
  (option) => option.value !== OperationType.Read,
);

// Listed rather than derived so a new event stays out of the admin picker until someone decides
// another account's rows of it are the admin's business.
export const ADMIN_EVENT_CODES: AuditEventCode[] = [
  AuditEventCode.UserCreated,
  AuditEventCode.UserDeleted,
  AuditEventCode.UserSoftDeleted,
  AuditEventCode.UserRestored,
  AuditEventCode.UserRenamed,
  AuditEventCode.UserEmailChanged,
  AuditEventCode.UserPasswordChanged,
  AuditEventCode.UserQuotaChanged,
  AuditEventCode.UserRoleGranted,
  AuditEventCode.UserRoleRevoked,
  AuditEventCode.UserLockedOut,
  AuditEventCode.UserLockoutLifted,
  AuditEventCode.UserTwoFactorEnabled,
  AuditEventCode.UserTwoFactorDisabled,
  AuditEventCode.UserLogin,
  AuditEventCode.UserLoginFailed,
  AuditEventCode.UserLogout,
  AuditEventCode.UserSessionRevoked,
  AuditEventCode.JobRequeued,
  AuditEventCode.JobsRequeuedBulk,
  AuditEventCode.IncidentResolved,
  AuditEventCode.AdminSettingChanged,
  AuditEventCode.AuditLogsPurged,
];

export const SOURCE_OPTIONS: { icon: string; label: string; value: LogSource }[] = [
  { icon: SOURCE_ICONS[LogSource.API], label: "App", value: LogSource.API },
  { icon: SOURCE_ICONS[LogSource.Trigger], label: "Database", value: LogSource.Trigger },
  { icon: SOURCE_ICONS[LogSource.System], label: "System", value: LogSource.System },
];
