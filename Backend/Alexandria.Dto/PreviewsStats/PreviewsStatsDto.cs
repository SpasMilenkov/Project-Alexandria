using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.TranspilationStats;

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

// Job-based stats over PreviewJob rows (discrete preview work items), mirroring
// the transpilation stats shape. Failure rate is computed over terminal jobs:
//   Total = Ready + Partial + Failed (Cancelled excluded)
//   Failed = Failed
public sealed record PreviewJobRatePoint(DateTime BucketStart, int Total, int Failed)
{
    public double FailureRate => Total == 0 ? 0 : Math.Round(100.0 * Failed / Total, 2);
}

public sealed record PreviewJobVolumePoint(DateTime BucketStart, int Count);

public sealed record PreviewJobDurationStats(
    double AvgMinutes,
    double P50Minutes,
    double P90Minutes,
    long SampleCount);

public sealed record PreviewJobOverviewResponse(
    IReadOnlyList<JobStatusCount> StatusCounts,
    PreviewJobDurationStats? Duration);

public sealed record PreviewJobTrendResponse(
    DateTime From,
    DateTime To,
    PreviewStatsBucket Bucket,
    IReadOnlyList<PreviewJobRatePoint> FailureRate,
    IReadOnlyList<PreviewJobVolumePoint> Volume);