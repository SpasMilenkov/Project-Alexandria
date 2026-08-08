namespace Alexandria.Dto.Enrichment;

/// <summary>
/// Live count of queued (dispatched) batch-files, split by backbone and file status.
/// </summary>
public sealed class EnrichmentQueueDepthDto
{
    public required string Backbone { get; init; }
    public required string Status { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// A batch-file that has been pending longer than the configured threshold inside a
/// non-terminal (dispatched) batch.
/// </summary>
public sealed class EnrichmentStuckDto
{
    public Guid FileId { get; init; }
    public Guid BatchId { get; init; }
    public required string Backbone { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Recent batch with the number of files that reached a terminal outcome.
/// </summary>
public sealed class EnrichmentBatchDto
{
    public Guid Id { get; init; }
    public required string Backbone { get; init; }
    public required string Status { get; init; }
    public DateTime? DispatchedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int FilesCompleted { get; init; }
    public int FilesTotal { get; init; }
}

/// <summary>
/// Time-series point for terminal batch-file outcomes, bucketed per backbone via Postgres
/// <c>date_trunc</c>. Failures count <c>Failed</c> + <c>MissingOutput</c>; the rate is among
/// completed attempts only.
/// </summary>
public sealed record EnrichmentFailureRatePointDto(
    DateTime Bucket,
    string Backbone,
    int Failed,
    int Succeeded,
    int Total)
{
    public double FailureRate => Total == 0 ? 0 : (double)Failed / Total;
}

/// <summary>
/// Per-backbone duration stats over terminal batch-files, where duration =
/// <c>CompletedAt - CreatedAt</c>. Median is computed in SQL via
/// <c>PERCENTILE_CONT(0.5)</c> — there is no LINQ median and client-side eval would pull
/// every row into memory.
/// </summary>
public sealed record EnrichmentDurationDto(
    string Backbone,
    double AverageSeconds,
    double MedianSeconds,
    double MaxSeconds);

/// <summary>
/// Job count per whole-hour bucket of <c>CreatedAt</c>.
/// </summary>
public sealed record EnrichmentVolumePointDto(DateTime Hour, int Count);

public enum EnrichmentBucket
{
    Hour,
    Day,
}