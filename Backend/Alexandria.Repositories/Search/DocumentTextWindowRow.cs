namespace Alexandria.Repositories.Search;

/// <summary>
/// Raw row of the first-match query: the text's state, whether the text matched, and the excerpts
/// Postgres cut from the stretch around the first word it found.
/// </summary>
internal sealed class DocumentTextWindowRow
{
    public int Status { get; set; }
    public bool IsTruncated { get; set; }
    public bool IsMatch { get; set; }
    public string? Headline { get; set; }
}