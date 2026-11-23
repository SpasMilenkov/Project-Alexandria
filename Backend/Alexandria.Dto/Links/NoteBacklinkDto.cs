namespace Alexandria.Dto.Links;

/// <summary>A note that links to the item, with the line the link sits on.</summary>
public sealed record NoteBacklinkDto
{
    public required Guid FileId { get; init; }
    public required string Name { get; init; }
    public Guid? DirectoryId { get; init; }
    public required DateTime UpdatedAt { get; init; }

    /// <summary>Markdown; links in it are shown with their labels or live names.</summary>
    public required string Snippet { get; init; }
}
