using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

/// <summary>
/// What counts as someone's own library in the audit log. Admins look after accounts, not
/// libraries, so rows like these never reach a reader who neither did them nor owns the data.
/// </summary>
public static class AuditPersonalContent
{
    public static readonly IReadOnlyList<EntityType> EntityTypes =
    [
        EntityType.File,
        EntityType.FileVersion,
        EntityType.Directory,
        EntityType.Tag,
        EntityType.Preview,
        EntityType.ShareLink,
        EntityType.Playlist,
        EntityType.Policy,
        EntityType.FileTag,
    ];

    // A user's own settings share the Setting entity type with system settings, so they are told
    // apart by event.
    public static readonly IReadOnlyList<AuditEventCode> EventCodes = [AuditEventCode.SettingChanged];
}
