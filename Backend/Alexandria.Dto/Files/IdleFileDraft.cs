namespace Alexandria.Dto.Files;

/// <summary>
/// A draft the idle sweep can turn into a version, without its text: each checkpoint reads that
/// on its own, so a batch never holds every draft in memory at once.
/// </summary>
public sealed record IdleFileDraft
{
    public required Guid FileId { get; init; }
    public required Guid BaseVersionId { get; init; }

    /// <summary>The revision the sweep saw; a draft written since is left to its editor.</summary>
    public required Guid Revision { get; init; }

    public required Guid OwnerId { get; init; }
}
