using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files;

/// <summary>
/// Where a search's words appear in a file's document text, for the search preview pane.
/// </summary>
public sealed class ContentMatchesResponse
{
    /// <summary>
    /// The outcome of reading the text, or null when it has not been read yet or never will be
    /// (unsupported type, or encrypted by its owner).
    /// </summary>
    public DocumentTextStatus? Status { get; init; }

    /// <summary>True when only the start of the document is searchable.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>Up to three excerpts around the matches, in document order. Empty when nothing matched.</summary>
    public IReadOnlyList<ContentMatchFragment> Fragments { get; init; } = [];
}