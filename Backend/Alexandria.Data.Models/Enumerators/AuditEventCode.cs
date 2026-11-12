namespace Alexandria.Data.Models.Enumerators;

public enum AuditEventCode
{
    // File events
    FileCreated,
    FileRenamed,
    FileMoved,
    FileSoftDeleted,
    FileRestored,
    FileDeletedPermanently,

    // FileVersion events
    FileVersionCreated,
    FileVersionSoftDeleted,
    FileVersionRestored,
    FileVersionDeletedPermanently,

    // Directory events
    DirectoryCreated,
    DirectoryRenamed,
    DirectoryMoved,
    DirectorySoftDeleted,
    DirectoryRestored,
    DirectoryDeletedPermanently,

    // Preview events
    PreviewCreated,
    PreviewSoftDeleted,
    PreviewRestored,
    PreviewDeletedPermanently,

    // Tag events
    TagCreated,
    TagRenamed,
    TagUpdated,
    TagSoftDeleted,
    TagRestored,
    TagDeletedPermanently,

    // User events
    UserCreated,
    UserDeleted,
    UserLogin,
    UserEmailChanged,
    UserPasswordChanged,
    UserLockedOut,
    UserLockoutLifted,
    UserTwoFactorEnabled,
    UserTwoFactorDisabled,

    // Sentinel
    Unknown,

    // Values below are appended after the sentinel because the frontend mirrors ordinals

    // User lifecycle
    UserSoftDeleted,
    UserRestored,
    UserRenamed,
    UserQuotaChanged,

    // File changes
    FileActiveVersionChanged,
    FileTypeChanged,

    // Tag changes
    TagReparented,

    // Custom track covers
    TrackCoverSet,
    TrackCoverRemoved,

    // Authentication
    UserLoginFailed,
    UserLogout,

    // Access
    FileDownloaded,
    FileVersionDownloaded,
    FilesDownloadedBulk,

    // Bulk summaries
    FilesMovedBulk,
    FilesDeletedBulk,
    FilesCopied,

    // Admin actions
    JobRequeued,
    JobsRequeuedBulk,
    IncidentResolved,

    // Media metadata and lyrics edits
    MediaMetadataEdited,
    MediaMetadataEditedBulk,
    LyricsUploaded,
    LyricsDeleted,
    LyricsProviderChanged,
    LyricsRefetchQueued,

    // Share links
    ShareLinkCreated,
    ShareLinkRevoked,
    ShareLinkAccessed,
    ShareLinkVersionChanged,

    // Roles
    UserRoleGranted,
    UserRoleRevoked,

    // File tags
    FileTagApplied,
    FileTagRemoved,

    // Playlists
    PlaylistCreated,
    PlaylistRenamed,
    PlaylistSoftDeleted,
    PlaylistRestored,
    PlaylistDeletedPermanently,
    PlaylistItemsChanged,

    // Directory policies
    PolicyCreated,
    PolicyUpdated,
    PolicyDeleted,
    PolicyRuleCreated,
    PolicyRuleUpdated,
    PolicyRuleDeleted,

    // Settings
    SettingChanged,
    AdminSettingChanged,

    // Sessions and retention
    UserSessionRevoked,
    AuditLogsPurged
}