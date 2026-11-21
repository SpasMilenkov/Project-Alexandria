namespace Alexandria.Repositories.Search;

/// <summary>
/// Raw row of the content-matches query: the text's state and, when it matched, the excerpts
/// Postgres cut around the matches.
/// </summary>
internal sealed class DocumentTextHeadlineRow
{
    public int Status { get; set; }
    public bool IsTruncated { get; set; }
    public string? Headline { get; set; }
}