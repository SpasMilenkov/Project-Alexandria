namespace Alexandria.Dto.Tags;

/// <summary>
/// Owner scope for tag searches. The requesting user's id is always enforced as a
/// privacy boundary (tags of other users are never returned); this only decides which
/// tags are visible on top of that boundary.
/// </summary>
public enum OwnerScope
{
    /// <summary>Tags owned by the requesting user (default).</summary>
    User = 0,

    /// <summary>System-seeded vocabulary tags (genre/mood taxonomy).</summary>
    System = 1,

    /// <summary>Requesting user's tags plus the system vocabulary.</summary>
    All = 2,
}