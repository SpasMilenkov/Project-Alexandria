using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;
using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Services.Monitoring;

public class PreviewStatsService(
    IPreviewRepository previewRepository,
    IJobRepository jobRepository) : IPreviewStatsService
{
    private static readonly JobType[] PreviewJobTypes = [JobType.MediaPreview, JobType.DocumentPreview];

    // Terminal = the job finished with an outcome; Cancelled is a user decision,
    // not a system outcome, so it stays out of the rate denominator entirely.
    private static readonly JobStatus[] TerminalStatuses =
    [
        JobStatus.Ready,
        JobStatus.Partial,
        JobStatus.Failed,
    ];

    public const int DefaultDurationWindowDays = 30;

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

    public async Task<PreviewJobOverviewResponse> GetJobOverviewAsync(
        JobType? type, int durationWindowDays, CancellationToken ct)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-durationWindowDays);

        var types = type.HasValue ? [type.Value] : PreviewJobTypes;
        var counts = new List<JobStatusCount>();
        foreach (var jobType in types)
            counts.AddRange(await jobRepository.GetStatusCountsAsync(jobType, ct));

        var window = (await jobRepository.GetJobsTouchingWindowAsync(from, to, ct))
            .Where(j => types.Contains(j.Type))
            .ToList();

        counts = counts
            .GroupBy(c => c.Status)
            .Select(g => new JobStatusCount(g.Key, g.Sum(c => c.Count)))
            .ToList();

        return new PreviewJobOverviewResponse(counts, DurationStats(window));
    }

    public async Task<PreviewJobTrendResponse> GetJobTrendAsync(
        JobType? type, DateTime fromUtc, DateTime toUtc,
        PreviewStatsBucket bucket, CancellationToken ct)
    {
        var types = type.HasValue ? [type.Value] : PreviewJobTypes;
        var jobs = (await jobRepository.GetJobsTouchingWindowAsync(fromUtc, toUtc, ct))
            .Where(j => types.Contains(j.Type))
            .ToList();

        var failureRate = jobs
            .Where(j => TerminalStatuses.Contains(j.Status) && j.CompletedAt != null)
            .GroupBy(j => BucketStart(j.CompletedAt!.Value, bucket))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var total = g.Count();
                var failed = g.Count(j => j.Status == JobStatus.Failed);
                return new PreviewJobRatePoint(g.Key, total, failed);
            })
            .ToList();

        var volume = jobs
            .GroupBy(j => BucketStart(j.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g => new PreviewJobVolumePoint(g.Key, g.Count()))
            .ToList();

        return new PreviewJobTrendResponse(fromUtc, toUtc, bucket, failureRate, volume);
    }

    private static PreviewJobDurationStats? DurationStats(IEnumerable<Job> windowJobs)
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

        return new PreviewJobDurationStats(
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

    private static DateTime BucketStart(DateTime timestamp, PreviewStatsBucket bucket) =>
        bucket == PreviewStatsBucket.Day
            ? new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0,
                DateTimeKind.Utc)
            : new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0,
                DateTimeKind.Utc);
}