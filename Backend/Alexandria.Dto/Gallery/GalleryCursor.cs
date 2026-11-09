namespace Alexandria.Dto.Gallery;

/// <summary>
/// The sort key of the last photo on a page. The next page starts strictly after it, so the
/// file id breaks ties between photos that share a timestamp, such as a burst.
/// </summary>
public sealed record GalleryCursor
{
    public required DateTime SortDate { get; init; }
    public required Guid FileId { get; init; }
}