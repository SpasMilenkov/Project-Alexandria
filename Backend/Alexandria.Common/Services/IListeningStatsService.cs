using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Common.Services;

/// <summary>On-demand listening-stats pipeline: fetch once, compute, curate, return.</summary>
public interface IListeningStatsService
{
    /// <summary>
    /// Builds the curated Wrapped deck for the user's closed sessions in
    /// <c>[fromUtc, toUtc)</c>. Live computation, no persistence.
    /// </summary>
    Task<WrappedDeckResponse> GetWrappedAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Builds day/month aggregates for the user's closed sessions in
    /// <c>[fromUtc, toUtc)</c>. Direct group-by, no new entity.
    /// </summary>
    Task<TimelineResponse> GetTimelineAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
