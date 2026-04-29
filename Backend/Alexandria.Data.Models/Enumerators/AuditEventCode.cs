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
    Unknown
}