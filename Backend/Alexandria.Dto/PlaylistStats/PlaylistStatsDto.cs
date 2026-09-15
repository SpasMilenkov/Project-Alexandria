using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Dto.PlaylistStats;

public sealed record PlaylistOverviewResponse(
    IReadOnlyList<JobStatusCount> StatusCounts,
    TranspilationDurationStats? Duration);

public sealed record PlaylistTrendResponse(
    DateTime From,
    DateTime To,
    StatsBucket Bucket,
    IReadOnlyList<TranspilationRatePoint> FailureRate,
    IReadOnlyList<TranspilationVolumePoint> Volume);