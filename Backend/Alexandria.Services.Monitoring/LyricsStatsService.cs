using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.LyricsStats;

namespace Alexandria.Services.Monitoring;

public class LyricsStatsService(ITrackLyricsRepository lyricsRepository) : ILyricsStatsService
{
    private static readonly LyricsStatus[] TerminalStatuses =
    [
        LyricsStatus.Fetched,
        LyricsStatus.NoMatch,
        LyricsStatus.FetchFailed,
    ];

    public async Task<LyricsOverviewResponse> GetOverviewAsync(CancellationToken ct)
    {
        var statusCounts = await lyricsRepository.GetStatusCountsAsync(ct);
        var providers = await lyricsRepository.GetProviderBreakdownAsync(ct);
        var avgConfidence = await lyricsRepository.GetAvgConfidenceAsync(ct);

        var fetchedTotal = statusCounts
            .Where(c => c.Status == LyricsStatus.Fetched)
            .Select(c => c.Count)
            .FirstOrDefault();

        return new LyricsOverviewResponse(statusCounts, providers, fetchedTotal, avgConfidence);
    }

    public async Task<LyricsTrendResponse> GetTrendAsync(DateTime fromUtc, DateTime toUtc,
        LyricsStatsBucket bucket, CancellationToken ct)
    {
        var rows = await lyricsRepository.GetCreatedBetweenAsync(fromUtc, toUtc, ct);

        var points = rows
            .GroupBy(l => BucketStart(l.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var terminal = g.Where(l => TerminalStatuses.Contains(l.Status)).ToList();
                return new LyricsRatePoint(
                    g.Key,
                    terminal.Count,
                    terminal.Count(l => l.Status == LyricsStatus.Fetched),
                    terminal.Count(l => l.Status == LyricsStatus.FetchFailed));
            })
            .ToList();

        return new LyricsTrendResponse(fromUtc, toUtc, bucket, points);
    }

    private static DateTime BucketStart(DateTime timestamp, LyricsStatsBucket bucket) =>
        bucket == LyricsStatsBucket.Day
            ? new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0,
                DateTimeKind.Utc)
            : new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0,
                DateTimeKind.Utc);
}