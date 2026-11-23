namespace Alexandria.Dto.Notes;

/// <summary>The start of a note's stored body text, read for its excerpt on the Notes page.</summary>
public sealed record NoteTextRow
{
    public required Guid FileId { get; init; }
    public required string Name { get; init; }
    public required string Text { get; init; }
}
