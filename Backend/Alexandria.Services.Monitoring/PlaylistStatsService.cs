using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PlaylistStats;
using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Services.Monitoring;

public class PlaylistStatsService(IJobRepository jobRepository) : IPlaylistStatsService
{
    // Playlist runs are binary (Ready/Failed, never Partial). Rows are
    // filtered by job type in the query; duration and bucket math stays in
    // memory per the repository guidance.
    private static readonly JobStatus[] TerminalStatuses =
    [
        JobStatus.Ready,
        JobStatus.Failed,
    ];

    public const int DefaultDurationWindowDays = 30;

    public async Task<PlaylistOverviewResponse> GetOverviewAsync(int durationWindowDays,
        CancellationToken ct)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-durationWindowDays);

        var statusCounts = await jobRepository.GetStatusCountsAsync(JobType.PlaylistSync, ct);
        var window = await jobRepository.GetJobsTouchingWindowAsync(
            from, to, ct, JobType.PlaylistSync);

        return new PlaylistOverviewResponse(statusCounts, DurationStats(window));
    }

    public async Task<PlaylistTrendResponse> GetTrendAsync(DateTime fromUtc, DateTime toUtc,
        StatsBucket bucket, CancellationToken ct)
    {
        var runs = await jobRepository.GetJobsTouchingWindowAsync(
            fromUtc, toUtc, ct, JobType.PlaylistSync);

        var failureRate = runs
            .Where(j => TerminalStatuses.Contains(j.Status) && j.CompletedAt != null)
            .GroupBy(j => BucketStart(j.CompletedAt!.Value, bucket))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var total = g.Count();
                var failed = g.Count(j => j.Status == JobStatus.Failed);
                return new TranspilationRatePoint(g.Key, total, failed);
            })
            .ToList();

        var volume = runs
            .GroupBy(j => BucketStart(j.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g => new TranspilationVolumePoint(g.Key, g.Count()))
            .ToList();

        return new PlaylistTrendResponse(fromUtc, toUtc, bucket, failureRate, volume);
    }

    private static TranspilationDurationStats? DurationStats(
        IEnumerable<Job> windowJobs)
    {
        var durationsMinutes = windowJobs
            .Where(j => TerminalStatuses.Contains(j.Status)
                        && j.StartedAt != null && j.CompletedAt != null)
            .Select(j => (j.CompletedAt!.Value - j.StartedAt!.Value).TotalMinutes)
            .Where(minutes => minutes >= 0)
            .OrderBy(minutes => minutes)
            .ToList();

        if (durationsMinutes.Count == 0)
            return null;

        return new TranspilationDurationStats(
            AvgMinutes: Math.Round(durationsMinutes.Average(), 2),
            P50Minutes: Math.Round(Percentile(durationsMinutes, 0.5), 2),
            P90Minutes: Math.Round(Percentile(durationsMinutes, 0.9), 2),
            SampleCount: durationsMinutes.Count);
    }

    private static double Percentile(List<double> sortedAscending, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sortedAscending.Count) - 1;
        if (index < 0)
            index = 0;
        return sortedAscending[index];
    }

    private static DateTime BucketStart(DateTime timestamp, StatsBucket bucket) =>
        bucket == StatsBucket.Day
            ? new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0,
                DateTimeKind.Utc)
            : new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0,
                DateTimeKind.Utc);
}