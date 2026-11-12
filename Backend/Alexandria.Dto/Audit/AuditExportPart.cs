namespace Alexandria.Dto.Audit;

/// <summary>
/// One file of a split export. The bounds are row positions rather than page numbers, so each part
/// downloads the same rows whenever and in whatever order it is fetched.
/// </summary>
public sealed class AuditExportPart
{
    public required int Index { get; init; }
    public required int Rows { get; init; }

    /// <summary>The part's newest row, included.</summary>
    public required DateTimeOffset StartAt { get; init; }

    public required Guid StartId { get; init; }

    /// <summary>The next part's newest row, left out; absent on the last part.</summary>
    public DateTimeOffset? EndAt { get; init; }

    public Guid? EndId { get; init; }

    /// <summary>When the part's oldest row was written, for labelling the part.</summary>
    public required DateTimeOffset OldestAt { get; init; }
}
