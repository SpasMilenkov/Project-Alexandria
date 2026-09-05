using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files.Streaming.Lyrics;

namespace Alexandria.Services.Streaming;

public class TrackLyricsService(
    IUnitOfWork unitOfWork,
    IPublisherService publisher) : ITrackLyricsService
{
    /// <inheritdoc/>
    public async Task<LyricsDto> GetLyricsForMediaAsync(Guid jobId, Guid userId, CancellationToken ct = default)
    {
        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId && l.DeletedAt == null, ct);

        if (lyrics is not null) return lyrics.ToDto();

        await QueueLyricsRefetchAsync(jobId, userId, ct);
        return new LyricsDto
        {
            Status = LyricsStatus.PendingFetch,
            Id = lyrics?.Id ?? Guid.Empty
        };
    }

    /// <inheritdoc/>
    public async Task QueueLyricsRefetchAsync(Guid jobId, Guid userId, CancellationToken ct = default)
    {
        var existing = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId && l.DeletedAt == null, ct);

        if (existing is not null)
        {
            if (existing.SourceProvider == LyricsProvider.Manual)
                throw new InvalidOperationException(
                    "Cannot queue a refetch for user-submitted lyrics. Remove them first.");

            existing.Status = LyricsStatus.PendingFetch;
            existing.FetchedAt = null;
            unitOfWork.Lyrics.Update(existing);
            await unitOfWork.SaveChangesAsync(ct);

            await RequeueAndPublishAsync(existing.JobId, "lyrics.job", ct);
            return;
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Queued,
            Type = JobType.LyricsFetch,
            UserId = userId
        };
        await unitOfWork.Jobs.AddAsync(job, ct);

        var lyrics = await unitOfWork.Lyrics.AddAsync(new TrackLyrics
        {
            TranspilationJobId = jobId,
            Status = LyricsStatus.PendingFetch,
            JobId = job.Id
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await PublishOrFailAsync(job.Id, "lyrics.job", ct);
    }

    // Requeues the linked job in place (same id, clean queued lifecycle) and
    // publishes it. Mirrors the preview dispatch requeue semantics.
    private async Task RequeueAndPublishAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Queued, progress: 0, ct: ct);
        await unitOfWork.Jobs.ClearErrorAsync(jobId, ct);
        await PublishOrFailAsync(jobId, routingKey, ct);
    }

    private async Task PublishOrFailAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(Encoding.UTF8.GetBytes(jobId.ToString()), routingKey);
        }
        catch (Exception)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: "publish failed", ct: ct);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveLyricsAsync(Guid lyricsId, Guid userId, CancellationToken ct = default)
    {
        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
                         l => l.Id == lyricsId && l.TranspilationJob!.UserId == userId, ct)
                     ?? throw new LyricsNotFoundException(lyricsId);

        lyrics.DeletedAt = DateTime.UtcNow;
        unitOfWork.Lyrics.Update(lyrics);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UploadLyricsAsync(
        Guid jobId,
        Guid userId,
        string? plainLyrics,
        string? syncedLyrics,
        CancellationToken ct = default)
    {
        if (plainLyrics is null && syncedLyrics is null)
            throw new ArgumentException("At least one of plain or synced lyrics must be provided.");

        var existing = await unitOfWork.Lyrics.FirstOrDefaultAsync(
            l => l.TranspilationJobId == jobId && l.TranspilationJob!.UserId == userId, ct);

        if (existing is not null)
        {
            existing.PlainLyrics = plainLyrics;
            existing.SyncedLyrics = syncedLyrics;
            existing.SourceProvider = LyricsProvider.Manual;
            existing.Status = LyricsStatus.Fetched;
            existing.Cached = false;
            existing.ProviderTrackId = null;
            existing.ConfidenceScore = null;
            existing.FetchedAt = DateTime.UtcNow;
            unitOfWork.Lyrics.Update(existing);

            // Manual upload is a completed fetch by definition: the linked job
            // reflects the user-supplied outcome instead of worker output.
            await unitOfWork.Jobs.UpdateStatusAsync(
                existing.JobId, JobStatus.Ready, progress: 100, ct: ct);
            await unitOfWork.Jobs.ClearErrorAsync(existing.JobId, ct);
        }
        else
        {
            var job = new Job
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Ready,
                ProgressPercent = 100,
                CompletedAt = DateTime.UtcNow,
                Type = JobType.LyricsFetch,
                UserId = userId
            };
            await unitOfWork.Jobs.AddAsync(job, ct);

            await unitOfWork.Lyrics.AddAsync(new TrackLyrics
            {
                TranspilationJobId = jobId,
                PlainLyrics = plainLyrics,
                SyncedLyrics = syncedLyrics,
                SourceProvider = LyricsProvider.Manual,
                Status = LyricsStatus.Fetched,
                Cached = false,
                FetchedAt = DateTime.UtcNow,
                JobId = job.Id
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task ChangeProviderAsync(
        Guid lyricsId,
        Guid userId,
        LyricsProvider provider,
        CancellationToken ct = default)
    {
        if (provider == LyricsProvider.Manual)
            throw new ArgumentException("Use UploadLyricsAsync to submit user lyrics.");

        var lyrics = await unitOfWork.Lyrics.FirstOrDefaultAsync(
                         l => l.Id == lyricsId && l.TranspilationJob!.UserId == userId, ct)
                     ?? throw new LyricsNotFoundException(lyricsId);

        lyrics.SourceProvider = provider;
        lyrics.Status = LyricsStatus.PendingFetch;
        lyrics.PlainLyrics = null;
        lyrics.SyncedLyrics = null;
        lyrics.ProviderTrackId = null;
        lyrics.ConfidenceScore = null;
        lyrics.FetchedAt = null;
        lyrics.Cached = false;
        unitOfWork.Lyrics.Update(lyrics);
        await unitOfWork.SaveChangesAsync(ct);

        await RequeueAndPublishAsync(lyrics.JobId, "lyrics.change", ct);
    }
}