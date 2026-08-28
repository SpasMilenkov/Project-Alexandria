using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Common.Services;

public interface ITranspilationStatsService
{
    /// <summary>
    /// Current all-time status distribution plus duration statistics
    /// (avg / p50 / p90 minutes) over terminal jobs touching the last
    /// <paramref name="durationWindowDays"/>.
    /// </summary>
    Task<TranspilationOverviewResponse> GetOverviewAsync(int durationWindowDays,
        CancellationToken ct);

    /// <summary>
    /// Failure-rate and volume points bucketed by <paramref name="bucket"/>
    /// over the given UTC window. Rate denominator counts terminal jobs only
    /// (Ready + Partial + Failed); Cancelled is excluded.
    /// </summary>
    Task<TranspilationTrendResponse> GetTrendAsync(DateTime fromUtc, DateTime toUtc,
        StatsBucket bucket, CancellationToken ct);
}