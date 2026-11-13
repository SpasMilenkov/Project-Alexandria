namespace Alexandria.Dto.Users;

/// <summary>
/// One place for the avatar object key and its public URL, which nginx maps one to one onto
/// the images bucket. The two must stay in step or every avatar 404s.
/// </summary>
public static class AvatarPaths
{
    public const string KeyPrefix = "avatars";

    public static string OwnerPrefix(Guid userId) => $"{KeyPrefix}/{userId}/";

    public static string Key(Guid userId, Guid avatarId) => $"{OwnerPrefix(userId)}{avatarId}";

    public static string? Url(Guid userId, Guid? avatarId)
    {
        if (avatarId is null)
            return null;

        return $"/{Key(userId, avatarId.Value)}";
    }
}
