using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;

namespace Alexandria.Services.Monitoring;

public class PreviewStatsService(IPreviewRepository previewRepository) : IPreviewStatsService
{
    public async Task<PreviewsOverviewResponse> GetOverviewAsync(CancellationToken ct) =>
        new(await previewRepository.GetKindTotalsAsync(ct));

    public async Task<PreviewVolumeResponse> GetVolumeAsync(DateTime fromUtc, DateTime toUtc,
        PreviewStatsBucket bucket, CancellationToken ct)
    {
        var previews = await previewRepository.GetCreatedBetweenAsync(fromUtc, toUtc, ct);

        var points = previews
            .GroupBy(p => BucketStart(p.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g => new PreviewVolumePoint(
                g.Key,
                g.Count(p => p.Kind == PreviewKind.Thumbnail),
                g.Count(p => p.Kind == PreviewKind.Preview)))
            .ToList();

        return new PreviewVolumeResponse(fromUtc, toUtc, bucket, points);
    }

    private static DateTime BucketStart(DateTime timestamp, PreviewStatsBucket bucket) =>
        bucket == PreviewStatsBucket.Day
            ? new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0,
                DateTimeKind.Utc)
            : new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0,
                DateTimeKind.Utc);
}