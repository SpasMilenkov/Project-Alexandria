using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.TranspilationStats;

public enum StatsBucket
{
    Hour,
    Day
}

public sealed record JobStatusCount(JobStatus Status, int Count);

// Failure rate is computed over terminal jobs only:
//   Total = Ready + Partial + Failed   (Cancelled is excluded from the denominator)
//   Failed = Failed
public sealed record TranspilationRatePoint(DateTime BucketStart, int Total, int Failed)
{
    public double FailureRate => Total == 0 ? 0 : Math.Round(100.0 * Failed / Total, 2);
}

public sealed record TranspilationVolumePoint(DateTime BucketStart, int Count);

public sealed record TranspilationDurationStats(
    double AvgMinutes,
    double P50Minutes,
    double P90Minutes,
    long SampleCount);

public sealed record TranspilationOverviewResponse(
    IReadOnlyList<JobStatusCount> StatusCounts,
    TranspilationDurationStats? Duration);

public sealed record TranspilationTrendResponse(
    DateTime From,
    DateTime To,
    StatsBucket Bucket,
    IReadOnlyList<TranspilationRatePoint> FailureRate,
    IReadOnlyList<TranspilationVolumePoint> Volume);