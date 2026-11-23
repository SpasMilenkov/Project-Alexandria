namespace Alexandria.Dto.Links;

/// <summary>How many of the viewer's notes link to one item.</summary>
public sealed record BacklinkCount
{
    public required Guid TargetId { get; init; }
    public required int Count { get; init; }
}
