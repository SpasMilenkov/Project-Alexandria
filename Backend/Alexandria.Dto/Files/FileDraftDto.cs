namespace Alexandria.Dto.Files;

/// <summary>The editor's working text for a file, as stored between versions.</summary>
public sealed record FileDraftDto
{
    public required Guid FileId { get; init; }

    /// <summary>The version the draft was made from; it may no longer be the current one.</summary>
    public required Guid BaseVersionId { get; init; }

    public required Guid Revision { get; init; }
    public required string Content { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
