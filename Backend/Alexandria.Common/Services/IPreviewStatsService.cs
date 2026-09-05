using Alexandria.Data.Models.Enumerators;
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

    /// <summary>
    /// Job-based overview over discrete preview work items. When <paramref name="type"/>
    /// is null both preview job types are combined.
    /// </summary>
    Task<PreviewJobOverviewResponse> GetJobOverviewAsync(
        JobType? type, int durationWindowDays, CancellationToken ct);

    /// <summary>
    /// Job-based failure-rate + volume trend over the given UTC window.
    /// </summary>
    Task<PreviewJobTrendResponse> GetJobTrendAsync(
        JobType? type, DateTime fromUtc, DateTime toUtc,
        PreviewStatsBucket bucket, CancellationToken ct);
}