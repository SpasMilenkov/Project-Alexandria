namespace Alexandria.Dto.Gallery;

/// <summary>
/// Month counts for the whole library, newest first. Small enough to send in one response, and
/// enough for the client to size every section and the scrubber before loading any photo.
/// </summary>
public sealed record GalleryTimelineResponse
{
    public required IReadOnlyList<GalleryMonthDto> Months { get; init; }
    public required int Total { get; init; }
}

public sealed record GalleryMonthDto
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required int Count { get; init; }

    public static GalleryMonthDto FromRow(GalleryMonthBucketRow row) => new()
    {
        Year = row.Year,
        Month = row.Month,
        Count = row.Count,
    };
}