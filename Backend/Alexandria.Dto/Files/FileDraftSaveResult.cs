namespace Alexandria.Dto.Files;

/// <summary>What a draft write left behind.</summary>
public sealed record FileDraftSaveResult
{
    /// <summary>
    /// The revision to name on the next write, or null when the text matched the version it
    /// builds on and the draft was removed.
    /// </summary>
    public required Guid? Revision { get; init; }

    public required DateTime SavedAt { get; init; }

    /// <summary>The note's new name, when this write named a note still called "Untitled".</summary>
    public string? RenamedTo { get; init; }
}
