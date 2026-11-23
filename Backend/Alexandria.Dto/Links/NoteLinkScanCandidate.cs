namespace Alexandria.Dto.Links;

/// <summary>A note's current text that has not been read for links yet, and a version to read it through.</summary>
public sealed record NoteLinkScanCandidate
{
    public required Guid ContentObjectId { get; init; }
    public required Guid VersionId { get; init; }
    public required Guid OwnerId { get; init; }
}
