using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.PreviewsStats;

public enum PreviewStatsBucket
{
    Hour,
    Day
}

public sealed record PreviewKindTotals(PreviewKind Kind, long Count, long TotalSizeBytes);

public sealed record PreviewsOverviewResponse(IReadOnlyList<PreviewKindTotals> ByKind);

// Kind columns are explicit (Thumbnail/Preview) — the only two kinds that exist.
public sealed record PreviewVolumePoint(
    DateTime BucketStart,
    int Thumbnails,
    int Previews);

public sealed record PreviewVolumeResponse(
    DateTime From,
    DateTime To,
    PreviewStatsBucket Bucket,
    IReadOnlyList<PreviewVolumePoint> Points);