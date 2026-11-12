// Mirrors backend AuditEventCode ordinals (Alexandria.Data.Models.Enumerators.AuditEventCode).
// Append-only: new codes go after the last member, never in between.
export enum AuditEventCode {
  // File events
  FileCreated = 0,
  FileRenamed = 1,
  FileMoved = 2,
  FileSoftDeleted = 3,
  FileRestored = 4,
  FileDeletedPermanently = 5,

  // FileVersion events
  FileVersionCreated = 6,
  FileVersionSoftDeleted = 7,
  FileVersionRestored = 8,
  FileVersionDeletedPermanently = 9,

  // Directory events
  DirectoryCreated = 10,
  DirectoryRenamed = 11,
  DirectoryMoved = 12,
  DirectorySoftDeleted = 13,
  DirectoryRestored = 14,
  DirectoryDeletedPermanently = 15,

  // Preview events
  PreviewCreated = 16,
  PreviewSoftDeleted = 17,
  PreviewRestored = 18,
  PreviewDeletedPermanently = 19,

  // Tag events
  TagCreated = 20,
  TagRenamed = 21,
  TagUpdated = 22,
  TagSoftDeleted = 23,
  TagRestored = 24,
  TagDeletedPermanently = 25,

  // User events
  UserCreated = 26,
  UserDeleted = 27,
  UserLogin = 28,
  UserEmailChanged = 29,
  UserPasswordChanged = 30,
  UserLockedOut = 31,
  UserLockoutLifted = 32,
  UserTwoFactorEnabled = 33,
  UserTwoFactorDisabled = 34,

  // Sentinel
  Unknown = 35,

  // User lifecycle
  UserSoftDeleted = 36,
  UserRestored = 37,
  UserRenamed = 38,
  UserQuotaChanged = 39,

  // File changes
  FileActiveVersionChanged = 40,
  FileTypeChanged = 41,

  // Tag changes
  TagReparented = 42,

  // Custom track covers
  TrackCoverSet = 43,
  TrackCoverRemoved = 44,

  // Authentication
  UserLoginFailed = 45,
  UserLogout = 46,

  // Access
  FileDownloaded = 47,
  FileVersionDownloaded = 48,
  FilesDownloadedBulk = 49,

  // Bulk summaries
  FilesMovedBulk = 50,
  FilesDeletedBulk = 51,
  FilesCopied = 52,

  // Admin actions
  JobRequeued = 53,
  JobsRequeuedBulk = 54,
  IncidentResolved = 55,

  // Media metadata and lyrics edits
  MediaMetadataEdited = 56,
  MediaMetadataEditedBulk = 57,
  LyricsUploaded = 58,
  LyricsDeleted = 59,
  LyricsProviderChanged = 60,
  LyricsRefetchQueued = 61,

  // Share links
  ShareLinkCreated = 62,
  ShareLinkRevoked = 63,
  ShareLinkAccessed = 64,
  ShareLinkVersionChanged = 65,

  // Roles
  UserRoleGranted = 66,
  UserRoleRevoked = 67,

  // File tags
  FileTagApplied = 68,
  FileTagRemoved = 69,

  // Playlists
  PlaylistCreated = 70,
  PlaylistRenamed = 71,
  PlaylistSoftDeleted = 72,
  PlaylistRestored = 73,
  PlaylistDeletedPermanently = 74,
  PlaylistItemsChanged = 75,

  // Directory policies
  PolicyCreated = 76,
  PolicyUpdated = 77,
  PolicyDeleted = 78,
  PolicyRuleCreated = 79,
  PolicyRuleUpdated = 80,
  PolicyRuleDeleted = 81,

  // Settings
  SettingChanged = 82,
  AdminSettingChanged = 83,

  // Sessions and retention
  UserSessionRevoked = 84,
  AuditLogsPurged = 85,
}
