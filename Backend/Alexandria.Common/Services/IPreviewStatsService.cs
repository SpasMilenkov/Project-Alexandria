using Alexandria.Dto.PreviewsStats;

namespace Alexandria.Common.Services;

public interface IPreviewStatsService
{
    /// <summary>All-time artifact count and size totals per kind.</summary>
    Task<PreviewsOverviewResponse> GetOverviewAsync(CancellationToken ct);

    /// <summary>
    /// Artifacts created per bucket over the given UTC window, split by kind.
    /// </summary>
    Task<PreviewVolumeResponse> GetVolumeAsync(DateTime fromUtc, DateTime toUtc,
        PreviewStatsBucket bucket, CancellationToken ct);
}