namespace Alexandria.Dto.Gallery;

public sealed record GalleryMonthBucketRow
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required int Count { get; init; }
}