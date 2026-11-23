namespace Alexandria.Dto.Notes;

/// <summary>What a note's row on the Notes page shows beyond its file details.</summary>
public sealed record NoteSummaryDto
{
    public required Guid FileId { get; init; }

    /// <summary>
    /// The opening text after the title, as markdown with its links kept so they show names.
    /// Null until the body text has been read, and for notes with nothing after the title.
    /// </summary>
    public string? Excerpt { get; init; }

    /// <summary>How many of the viewer's other notes link to this one.</summary>
    public required int MentionCount { get; init; }
}
